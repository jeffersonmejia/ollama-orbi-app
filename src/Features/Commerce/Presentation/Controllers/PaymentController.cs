using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrbiApp.Models.Commerce;
using OrbiApp.Application.Features.Payments.Ports;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Data;

namespace OrbiApp.Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly IApplicationDataSession _context;
    private readonly IPayPhoneGateway _payPhoneService;
    private readonly IPayPalGateway _payPalService;
    private readonly ApplicationDbContext _database;

    public PaymentController(
        IApplicationDataSession context,
        IPayPhoneGateway payPhoneService,
        IPayPalGateway payPalService,
        ApplicationDbContext database)
    {
        _context = context;
        _payPhoneService = payPhoneService;
        _payPalService = payPalService;
        _database = database;
    }

    public async Task<IActionResult> CreateLink(int orderId)
    {
        var order = await _context.Set<PurchaseOrder>()
            .Include(o => o.Details)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == orderId);

        if (order == null) return NotFound();

        if (order.Total < 1.00m)
        {
            TempData["Error"] = "Cannot generate link: total is below $1.00.";
            return RedirectToAction("Cart", "Tienda");
        }

        string clientTransactionId = DateTime.Now.ToString("yyMMddHHmmssfff")[..15];
        string reference = $"Pedido Orbi #{order.PurchaseOrderId}";

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

        _context.Set<PaymentTransaction>().Add(payment);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = payment.PaymentTransactionId });
    }

    public async Task<IActionResult> CreatePayPalOrder(int orderId)
    {
        var order = await _context.Set<PurchaseOrder>()
            .Include(o => o.Details)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == orderId);

        if (order == null) return NotFound();

        if (order.Total < 1.00m)
        {
            TempData["Error"] = "Cannot process payment: total is below $1.00.";
            return RedirectToAction("Cart", "Tienda");
        }

        string reference = $"Pedido Orbi #{order.PurchaseOrderId}";

        var returnUrl = $"{Request.Scheme}://{Request.Host}/Payment/Success";
        var cancelUrl = $"{Request.Scheme}://{Request.Host}/Payment/Cancel";
        var result = await _payPalService.CreateOrderAsync(order.Total, reference, returnUrl, cancelUrl);

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

        _context.Set<PaymentTransaction>().Add(payment);
        await _context.SaveChangesAsync();

        return Redirect(result.ApprovalUrl);
    }

    public async Task<IActionResult> Success(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest("PayPal did not return an order token.");
        }

        await using var transaction = await _database.Database.BeginTransactionAsync();
        var paymentId = await _context.Set<PaymentTransaction>()
            .Where(p => (p.Provider == "PayPal" || p.Provider == "PayPalButton") && p.PayPalOrderId == token)
            .Select(p => new { p.PaymentTransactionId, p.PurchaseOrderId })
            .FirstOrDefaultAsync();

        if (paymentId == null) return NotFound();

        await _database.AcquirePaymentLockAsync("commerce-order", paymentId.PurchaseOrderId);

        var payment = await _context.Set<PaymentTransaction>()
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
            if (payment.PurchaseOrder.Status != "Paid")
            {
                payment.PurchaseOrder.Status = "Paid";
                await DescontarStockAsync(payment.PurchaseOrder);
            }
        }
        else
        {
            payment.Status = capture.Status;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return RedirectToAction(nameof(Details), new { id = payment.PaymentTransactionId });
    }

    public async Task<IActionResult> Cancel(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            var payment = await _context.Set<PaymentTransaction>()
                .FirstOrDefaultAsync(p => (p.Provider == "PayPal" || p.Provider == "PayPalButton") && p.PayPalOrderId == token);

            if (payment != null && payment.Status == "Pending")
            {
                payment.Status = "Canceled";
                await _context.SaveChangesAsync();
            }
        }

        TempData["Error"] = "PayPal payment was canceled.";
        return RedirectToAction("Index", "Tienda");
    }

    public async Task<IActionResult> Details(int id)
    {
        var payment = await _context.Set<PaymentTransaction>()
            .Include(p => p.PurchaseOrder)
            .ThenInclude(o => o.Details)
            .FirstOrDefaultAsync(p => p.PaymentTransactionId == id);

        if (payment == null) return NotFound();
        return View(payment);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> MarkAsPaid(int id)
    {
        await using var transaction = await _database.Database.BeginTransactionAsync();
        var orderId = await _context.Set<PaymentTransaction>()
            .Where(p => p.PaymentTransactionId == id)
            .Select(p => (int?)p.PurchaseOrderId)
            .FirstOrDefaultAsync();
        if (orderId == null) return NotFound();
        await _database.AcquirePaymentLockAsync("commerce-order", orderId.Value);
        var payment = await _context.Set<PaymentTransaction>()
            .Include(p => p.PurchaseOrder)
            .ThenInclude(o => o.Details)
            .FirstOrDefaultAsync(p => p.PaymentTransactionId == id);

        if (payment == null) return NotFound();

        if (payment.Status != "Paid")
        {
            payment.Status = "Paid";
            payment.ConfirmedAt = DateTime.UtcNow;
            if (payment.PurchaseOrder.Status != "Paid")
            {
                payment.PurchaseOrder.Status = "Paid";
                await DescontarStockAsync(payment.PurchaseOrder);
            }
            await _context.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> PayPalButton(int orderId)
    {
        var order = await _context.Set<PurchaseOrder>()
            .Include(o => o.Details)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == orderId);

        if (order == null) return NotFound();

        ViewBag.ClientId = _payPalService.PublicClientId;
        ViewBag.OrderId = order.PurchaseOrderId;
        ViewBag.Total = order.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        return View(order);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayPalButtonOrderJson(int orderId)
    {
        var order = await _context.Set<PurchaseOrder>()
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

        string reference = $"Pedido Orbi #{order.PurchaseOrderId}";

        var returnUrl = $"{Request.Scheme}://{Request.Host}/Payment/Success";
        var cancelUrl = $"{Request.Scheme}://{Request.Host}/Payment/Cancel";
        var result = await _payPalService.CreateOrderAsync(order.Total, reference, returnUrl, cancelUrl);

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

        _context.Set<PaymentTransaction>().Add(payment);
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
        await using var transaction = await _database.Database.BeginTransactionAsync();
        var orderId = await _context.Set<PaymentTransaction>()
            .Where(p => p.PaymentTransactionId == request.PaymentTransactionId)
            .Select(p => (int?)p.PurchaseOrderId)
            .FirstOrDefaultAsync();
        if (orderId == null) return Json(new { success = false, message = "Transaction not found." });
        await _database.AcquirePaymentLockAsync("commerce-order", orderId.Value);
        var payment = await _context.Set<PaymentTransaction>()
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

        if (payment.Status == "Paid")
        {
            await transaction.CommitAsync();
            return Json(new
            {
                success = true,
                redirectUrl = Url.Action("Details", "Payment", new { id = payment.PaymentTransactionId })
            });
        }

        var capture = await _payPalService.CaptureOrderAsync(request.PayPalOrderId);

        payment.PayPalCaptureId = capture.CaptureId;
        payment.GatewayResponse = capture.RawResponse;
        payment.ConfirmedAt = DateTime.UtcNow;

        if (capture.Status == "COMPLETED")
        {
            payment.Status = "Paid";
            if (payment.PurchaseOrder.Status != "Paid")
            {
                payment.PurchaseOrder.Status = "Paid";
                await DescontarStockAsync(payment.PurchaseOrder);
            }
        }
        else
        {
            payment.Status = capture.Status;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

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
            var stock = await _context.Set<Product>().FindAsync(detail.ProductId);
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
