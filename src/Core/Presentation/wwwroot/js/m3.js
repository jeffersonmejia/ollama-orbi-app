(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    initRipple();
    initSplash();
    initNavRailActive();
    initDialogs();
    initPasswordToggles();
    initRoleSummary();
  });

  function initRipple() {
    document.querySelectorAll(
      '.m3-filled-button, .m3-outlined-button, .m3-text-button, ' +
      '.m3-fab, .m3-nav-item, .m3-icon-btn, .m3-chip, ' +
      '.m3-list-item, .m3-pagination-btn'
    ).forEach(function (el) {
      el.addEventListener('click', function (e) {
        const rect = el.getBoundingClientRect();
        const size = Math.max(rect.width, rect.height);
        const x = e.clientX - rect.left - size / 2;
        const y = e.clientY - rect.top - size / 2;
        const ripple = document.createElement('span');
        ripple.className = 'm3-ripple';
        ripple.style.width = ripple.style.height = size + 'px';
        ripple.style.left = x + 'px';
        ripple.style.top = y + 'px';
        el.appendChild(ripple);
        ripple.addEventListener('animationend', function () {
          ripple.remove();
        });
      });
    });
  }

  function initSplash() {
    const splash = document.getElementById('m3Splash');
    if (!splash) return;

    const duration = parseInt(splash.getAttribute('data-duration')) || 1200;
    const minDisplay = 800;
    const startTime = Date.now();

    function hideSplash() {
      const elapsed = Date.now() - startTime;
      const remaining = Math.max(0, minDisplay - elapsed);
      setTimeout(function () {
        splash.classList.add('hidden');
        setTimeout(function () {
          splash.style.display = 'none';
        }, 500);
      }, remaining);
    }

    if (document.readyState === 'complete') {
      hideSplash();
    } else {
      window.addEventListener('load', hideSplash);
      setTimeout(hideSplash, duration);
    }
  }

  function initNavRailActive() {
    document.querySelectorAll('.m3-nav-item').forEach(function (item) {
      if (item.getAttribute('href') && window.location.pathname.includes(item.getAttribute('href'))) {
        item.classList.add('active');
      }
    });
  }

  function initDialogs() {
    document.querySelectorAll('[data-m3-dialog]').forEach(function (trigger) {
      trigger.addEventListener('click', function () {
        const dialogId = trigger.getAttribute('data-m3-dialog');
        const dialog = document.getElementById(dialogId);
        if (!dialog) return;
        showDialog(dialog);
      });
    });

    document.querySelectorAll('.m3-dialog-overlay').forEach(function (overlay) {
      overlay.addEventListener('click', function () {
        const dialog = overlay.nextElementSibling;
        if (dialog && dialog.classList.contains('m3-dialog')) {
          hideDialog(overlay, dialog);
        }
      });
    });

    document.querySelectorAll('.m3-dialog [data-dismiss]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        const dialog = btn.closest('.m3-dialog');
        const overlay = dialog && dialog.previousElementSibling;
        if (dialog && overlay && overlay.classList.contains('m3-dialog-overlay')) {
          hideDialog(overlay, dialog);
        }
      });
    });
  }

  window.showM3Dialog = function (dialogId) {
    const dialog = document.getElementById(dialogId);
    if (dialog) showDialog(dialog);
  };

  window.hideM3Dialog = function (dialogId) {
    const dialog = document.getElementById(dialogId);
    if (dialog) {
      const overlay = dialog.previousElementSibling;
      if (overlay && overlay.classList.contains('m3-dialog-overlay')) {
        hideDialog(overlay, dialog);
      }
    }
  };

  function showDialog(dialog) {
    const overlay = dialog.previousElementSibling;
    if (overlay && overlay.classList.contains('m3-dialog-overlay')) {
      overlay.classList.add('show');
    }
    dialog.classList.add('show');
    document.body.style.overflow = 'hidden';
  }

  function hideDialog(overlay, dialog) {
    overlay.classList.remove('show');
    dialog.classList.remove('show');
    document.body.style.overflow = '';
  }

  function initPasswordToggles() {
    document.querySelectorAll('.m3-password-toggle').forEach(function (btn) {
      btn.addEventListener('click', function () {
        const wrapper = btn.closest('.m3-password-wrapper');
        if (!wrapper) return;
        const input = wrapper.querySelector('input');
        if (!input) return;
        const isPassword = input.type === 'password';
        input.type = isPassword ? 'text' : 'password';
        const icons = btn.querySelectorAll('.m3-eye-icon, .m3-eye-off-icon');
        icons.forEach(function (icon) {
          icon.style.display = icon.classList.contains(isPassword ? 'm3-eye-off-icon' : 'm3-eye-icon')
            ? '' : 'none';
        });
      });
    });
  }

  function initRoleSummary() {
    const roleSelect = document.getElementById('Input_Role');
    const roleSummary = document.getElementById('roleSummary');
    if (!roleSelect || !roleSummary) return;

    const descriptions = {
      Usuario: 'Compra productos y consulta el estado de sus pedidos.',
      Vendedor: 'Perfil para gestionar la operación comercial de una tienda.',
      Repartidor: 'Consulta sus entregas asignadas y actualiza su estado.'
    };

    function updateRoleSummary() {
      roleSummary.textContent = descriptions[roleSelect.value] ||
        'Selecciona un rol para ver el tipo de acceso que tendrá el usuario.';
    }

    roleSelect.addEventListener('change', updateRoleSummary);
    updateRoleSummary();
  }

  window.showM3Snackbar = function (message, actionText, actionCallback, duration) {
    duration = duration || 4000;
    var existing = document.querySelector('.m3-snackbar');
    if (existing) existing.remove();

    var snackbar = document.createElement('div');
    snackbar.className = 'm3-snackbar';
    snackbar.setAttribute('role', 'alert');

    var msgSpan = document.createElement('span');
    msgSpan.textContent = message;
    snackbar.appendChild(msgSpan);

    if (actionText && actionCallback) {
      var actionBtn = document.createElement('button');
      actionBtn.className = 'm3-snackbar-action';
      actionBtn.textContent = actionText;
      actionBtn.addEventListener('click', function () {
        actionCallback();
        snackbar.classList.remove('show');
        setTimeout(function () { snackbar.remove(); }, 350);
      });
      snackbar.appendChild(actionBtn);
    }

    document.body.appendChild(snackbar);
    requestAnimationFrame(function () {
      snackbar.classList.add('show');
    });

    setTimeout(function () {
      snackbar.classList.remove('show');
      setTimeout(function () { snackbar.remove(); }, 350);
    }, duration);
  };

})();
