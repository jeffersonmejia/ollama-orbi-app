(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    initRipple();
    initSplash();
    initNavRailActive();
    initNavToggle();
    initDialogs();
    initDismissibleAlerts();
    initPasswordToggles();
    initRoleSummary();
    initNavigationProgress();
  });

  function initNavigationProgress() {
    const storageKey = 'dash-nav-progress';
    const root = document.documentElement;

    function finish() {
      root.classList.remove('dash-navigation-pending');
      try { window.sessionStorage.removeItem(storageKey); } catch (error) { }
    }

    document.addEventListener('click', function (event) {
      const link = event.target.closest('a[data-nav-progress="true"]');
      if (!link || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || link.target || link.hasAttribute('download')) return;

      let destination;
      try { destination = new URL(link.href, window.location.href); } catch (error) { return; }
      if (destination.origin !== window.location.origin || (destination.pathname === window.location.pathname && destination.search === window.location.search && destination.hash === window.location.hash)) return;

      root.classList.add('dash-navigation-pending');
      try { window.sessionStorage.setItem(storageKey, '1'); } catch (error) { }
      if (destination.pathname.replace(/\/$/, '').toLowerCase().endsWith('/delivery/index')) showStoreLoadingSkeleton();
    }, true);

    document.addEventListener('submit', function (event) {
      if (event.target.matches('.orbi-store-filters')) showStoreLoadingSkeleton();
    }, true);

    function showStoreLoadingSkeleton() {
      const main = document.querySelector('.m3-main');
      if (!main) return;
      main.setAttribute('aria-busy', 'true');
      main.replaceChildren();
      const skeleton = document.createElement('section');
      skeleton.className = 'orbi-store-loading-skeleton';
      skeleton.setAttribute('role', 'status');
      skeleton.setAttribute('aria-label', 'Cargando tiendas y productos');
      skeleton.innerHTML = `
        <div class="orbi-store-skeleton-filters" aria-hidden="true">
          <span class="m3-skeleton"></span><span class="m3-skeleton"></span><span class="m3-skeleton"></span><span class="m3-skeleton"></span><span class="m3-skeleton"></span>
        </div>
        <div class="orbi-store-skeleton-categories" aria-hidden="true">
          <span class="m3-skeleton"></span><span class="m3-skeleton"></span><span class="m3-skeleton"></span>
        </div>
        <div class="orbi-store-skeleton-products" aria-hidden="true">
          ${Array.from({ length: 9 }, () => '<span class="m3-skeleton"></span>').join('')}
        </div>`;
      main.appendChild(skeleton);
    }

    try {
      if (window.sessionStorage.getItem(storageKey) === '1') {
        root.classList.add('dash-navigation-pending');
        window.addEventListener('load', function () { window.setTimeout(finish, 180); }, { once: true });
        window.addEventListener('pageshow', function (event) { if (event.persisted) finish(); }, { once: true });
      }
    } catch (error) { }
  }

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
    const normalizePath = function (path) {
      const normalized = path.replace(/\/+$/, '');
      return normalized || '/';
    };
    const currentPath = normalizePath(window.location.pathname);

    document.querySelectorAll('.m3-nav-item').forEach(function (item) {
      const href = item.getAttribute('href');
      if (!href) return;

      const targetPath = normalizePath(new URL(href, window.location.origin).pathname);
      if (targetPath === currentPath) {
        item.classList.add('active');
      }
    });
  }

  function initDismissibleAlerts() {
    document.querySelectorAll('[data-orbi-dismiss-alert]').forEach(function (button) {
      button.addEventListener('click', function () {
        const alert = button.closest('.orbi-alert');
        if (alert) alert.remove();
      });
    });
  }

  function initNavToggle() {
    const layout = document.getElementById('m3-app-layout');
    const nav = document.getElementById('m3-nav-rail');
    const toggle = document.getElementById('m3-nav-toggle');
    const icon = toggle && toggle.querySelector('.material-symbols-outlined');
    if (!layout || !nav || !toggle || !icon) return;

    // Mueve el control de versiones anteriores del layout al encabezado del aside.
    let navHeader = nav.querySelector('.m3-nav-rail-header');
    const previousTopBar = toggle.closest('.m3-top-app-bar');
    if (!navHeader) {
      navHeader = document.createElement('div');
      navHeader.className = 'm3-nav-rail-header';
      nav.prepend(navHeader);
    }

    if (!navHeader.contains(toggle)) navHeader.appendChild(toggle);
    if (previousTopBar && !previousTopBar.querySelector('#m3-nav-toggle')) previousTopBar.remove();

    const storageKey = 'orbi-nav-compact:' + (layout.dataset.navPreferenceKey || 'anonymous');

    document.querySelectorAll('.m3-nav-item').forEach(function (item) {
      const profileLabel = item.querySelector('.orbi-profile-label > span');
      const label = profileLabel || Array.from(item.children).find(function (child) {
        return !child.classList.contains('material-symbols-outlined');
      });
      if (!label) return;

      const text = label.textContent.trim();
      if (text) {
        item.setAttribute('data-nav-label', text);
        item.setAttribute('title', text);
      }
    });

    function setCompact(compact) {
      layout.classList.toggle('nav-collapsed', compact);
      nav.inert = false;
      nav.setAttribute('aria-hidden', 'false');
      toggle.setAttribute('aria-expanded', String(!compact));
      toggle.setAttribute('aria-label', compact ? 'Expandir navegación' : 'Contraer navegación');
      toggle.setAttribute('title', compact ? 'Expandir navegación' : 'Contraer navegación');
      icon.textContent = compact ? 'left_panel_open' : 'left_panel_close';
    }

    try {
      let savedPreference = window.localStorage.getItem(storageKey);
      if (savedPreference === null) {
        const previousPreference = window.localStorage.getItem('orbi-nav-compact');
        if (previousPreference !== null) {
          window.localStorage.setItem(storageKey, previousPreference);
          window.localStorage.removeItem('orbi-nav-compact');
          savedPreference = previousPreference;
        }
      }
      setCompact(savedPreference === 'true');
    } catch (error) {
      setCompact(false);
    }

    toggle.addEventListener('click', function () {
      const compact = !layout.classList.contains('nav-collapsed');
      setCompact(compact);
      try {
        window.localStorage.setItem(storageKey, String(compact));
      } catch (error) {
        // El menú sigue funcionando aunque el navegador bloquee el almacenamiento local.
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
