using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SakilaApp.Data;
using SakilaApp.Models.Commerce;
using SakilaApp.Services.Payments;
using SakilaApp.Settings;

namespace SakilaApp.Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PayPhoneApiLinkService _payPhoneService;
    private readonly PayPalService _payPalService;
    private readonly PayPalSettings _payPalSettings;

    public PaymentController(
        ApplicationDbContext context,
        PayPhoneApiLinkService payPhoneService,
        PayPalService payPalService,
        IOptions<PayPalSettings> payPalSettings)
    {
        _context = context;
        _payPhoneService = payPhoneService;
        _payPalService = payPalService;
        _payPalSettings = payPalSettings.Value;
    }

    public async Task<IActionResult> CreateLink(int orderId)
    {
        var order = await _context.PurchaseOrders
            .Include(o => o.Details)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == orderId);

        if (order == null) return NotFound();

        if (order.Total < 1.00m)
        {
            TempData["Error"] = "Cannot generate link: total is below $1.00.";
            return RedirectToAction("Cart", "Store");
        }

        string clientTransactionId = DateTime.Now.ToString("yyMMddHHmmssfff")[..15];
        string reference = $"Sakila Order #{order.PurchaseOrderId}";

        string link = await _payPhoneService.CreatePaymentLinkAsync(
            order.Total,
            clientTransactionId,
            reference);

        var payment = new PaymentTransaction
        {
            PurchaseOrderId = order.PurchaseOrderId,
            ClientTransactionId = clientTransactionId,
            Provider = "PayPhone",
            PayphonePaymentUrl = link,
            AmountInCents = ToCents(order.Total),
            Status = "Pending"
        };

        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = payment.PaymentTransactionId });
    }

    public async Task<IActionResult> CreatePayPalOrder(int orderId)
    {
        var order = await _context.PurchaseOrders
            .Include(o => o.Details)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == orderId);

        if (order == null) return NotFound();

        if (order.Total < 1.00m)
        {
            TempData["Error"] = "Cannot process payment: total is below $1.00.";
            return RedirectToAction("Cart", "Store");
        }

        string reference = $"Sakila Order #{order.PurchaseOrderId}";

        var result = await _payPalService.CreateOrderAsync(
    order.Total,
    reference);//,
    //order.PurchaseOrderId);

        var payment = new PaymentTransaction
        {
            PurchaseOrderId = order.PurchaseOrderId,
            ClientTransactionId = result.OrderId,
            Provider = "PayPal",
            PayPalOrderId = result.OrderId,
            PayPalApprovalUrl = result.ApprovalUrl,
            AmountInCents = ToCents(order.Total),
            Status = "Pending",
            GatewayResponse = result.RawResponse
        };

        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync();

        return Redirect(result.ApprovalUrl);
    }

    public async Task<IActionResult> Success(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest("PayPal did not return an order token.");
        }

        var payment = await _context.PaymentTransactions
            .Include(p => p.PurchaseOrder)
            .ThenInclude(o => o.Details)
            .FirstOrDefaultAsync(p => (p.Provider == "PayPal" || p.Provider == "PayPalButton") && p.PayPalOrderId == token);

        if (payment == null) return NotFound();

        if (payment.Status == "Paid")
        {
            return RedirectToAction(nameof(Details), new { id = payment.PaymentTransactionId });
        }

        var capture = await _payPalService.CaptureOrderAsync(token);

        payment.PayPalCaptureId = capture.CaptureId;
        payment.GatewayResponse = capture.RawResponse;
        payment.ConfirmedAt = DateTime.UtcNow;

        if (capture.Status == "COMPLETED")
        {
            payment.Status = "Paid";
            payment.PurchaseOrder.Status = "Paid";
            await DescontarStockAsync(payment.PurchaseOrder);
        }
        else
        {
            payment.Status = capture.Status;
        }

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = payment.PaymentTransactionId });
    }

    public async Task<IActionResult> Cancel(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            var payment = await _context.PaymentTransactions
                .FirstOrDefaultAsync(p => (p.Provider == "PayPal" || p.Provider == "PayPalButton") && p.PayPalOrderId == token);

            if (payment != null && payment.Status == "Pending")
            {
                payment.Status = "Canceled";
                await _context.SaveChangesAsync();
            }
        }

        TempData["Error"] = "PayPal payment was canceled.";
        return RedirectToAction("Index", "Store");
    }

    public async Task<IActionResult> Details(int id)
    {
        var payment = await _context.PaymentTransactions
            .Include(p => p.PurchaseOrder)
            .ThenInclude(o => o.Details)
            .FirstOrDefaultAsync(p => p.PaymentTransactionId == id);

        if (payment == null) return NotFound();
        return View(payment);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> MarkAsPaid(int id)
    {
        var payment = await _context.PaymentTransactions
            .Include(p => p.PurchaseOrder)
            .ThenInclude(o => o.Details)
            .FirstOrDefaultAsync(p => p.PaymentTransactionId == id);

        if (payment == null) return NotFound();

        if (payment.Status != "Paid")
        {
            payment.Status = "Paid";
            payment.ConfirmedAt = DateTime.UtcNow;
            payment.PurchaseOrder.Status = "Paid";
            await DescontarStockAsync(payment.PurchaseOrder);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> PayPalButton(int orderId)
    {
        var order = await _context.PurchaseOrders
            .Include(o => o.Details)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == orderId);

        if (order == null) return NotFound();

        ViewBag.ClientId = _payPalSettings.ClientId;
        ViewBag.OrderId = order.PurchaseOrderId;
        ViewBag.Total = order.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        return View(order);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayPalButtonOrderJson(int orderId)
    {
        var order = await _context.PurchaseOrders
            .Include(o => o.Details)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == orderId);

        if (order == null)
        {
            return Json(new
            {
                success = false,
                message = "Order not found."
            });
        }

        string reference = $"Sakila Order #{order.PurchaseOrderId}";

        var result = await _payPalService.CreateOrderAsync(
            order.Total,
            reference);

        var payment = new PaymentTransaction
        {
            PurchaseOrderId = order.PurchaseOrderId,
            ClientTransactionId = result.OrderId,
            Provider = "PayPalButton",
            PayPalOrderId = result.OrderId,
            PayPalApprovalUrl = result.ApprovalUrl,
            AmountInCents = ToCents(order.Total),
            Status = "Pending",
            GatewayResponse = result.RawResponse
        };

        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            paypalOrderId = result.OrderId,
            paymentTransactionId = payment.PaymentTransactionId
        });
    }

    [HttpPost]
    public async Task<IActionResult> CapturePayPalButtonOrderJson([FromBody] PayPalButtonCaptureRequest request)
    {
        var payment = await _context.PaymentTransactions
            .Include(p => p.PurchaseOrder)
            .ThenInclude(o => o.Details)
            .FirstOrDefaultAsync(p =>
                p.PaymentTransactionId == request.PaymentTransactionId &&
                p.PayPalOrderId == request.PayPalOrderId);

        if (payment == null)
        {
            return Json(new
            {
                success = false,
                message = "Transaction not found."
            });
        }

        var capture = await _payPalService.CaptureOrderAsync(request.PayPalOrderId);

        payment.PayPalCaptureId = capture.CaptureId;
        payment.GatewayResponse = capture.RawResponse;
        payment.ConfirmedAt = DateTime.UtcNow;

        if (capture.Status == "COMPLETED")
        {
            payment.Status = "Paid";
            payment.PurchaseOrder.Status = "Paid";
            await DescontarStockAsync(payment.PurchaseOrder);
        }
        else
        {
            payment.Status = capture.Status;
        }

        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            redirectUrl = Url.Action("Details", "Payment", new { id = payment.PaymentTransactionId })
        });
    }

    private async Task DescontarStockAsync(PurchaseOrder order)
    {
        foreach (var detail in order.Details)
        {
            var stock = await _context.FilmStocks.FindAsync(detail.FilmStockId);
            if (stock != null)
            {
                stock.Stock = Math.Max(0, stock.Stock - detail.Quantity);
            }
        }
    }

    private static int ToCents(decimal value)
    {
        return (int)Math.Round(value * 100, MidpointRounding.AwayFromZero);
    }
}

public class PayPalButtonCaptureRequest
{
    public string PayPalOrderId { get; set; } = string.Empty;
    public int PaymentTransactionId { get; set; }
}
