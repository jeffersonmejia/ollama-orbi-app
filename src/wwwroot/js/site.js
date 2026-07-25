// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Mejoras que pueden desplegarse como archivos estáticos sin recompilar la aplicación.
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    updateSessionNavigation();
    initRegisterSteps();
    initGoogleLogin();
  });

  function updateSessionNavigation() {
    document.querySelectorAll('.m3-nav-item[href*="/Account/Register"] span:last-child')
      .forEach(function (label) { label.textContent = 'Registro'; });

    var profileLink = document.querySelector('.m3-nav-item[href*="/Account/Manage"]');
    if (!profileLink || profileLink.querySelector('.orbi-profile-label')) return;

    var label = Array.from(profileLink.querySelectorAll('span'))
      .find(function (item) { return item.textContent.trim() === 'Perfil'; });
    if (!label) return;

    var role = detectRole();
    var wrapper = document.createElement('span');
    wrapper.className = 'orbi-profile-label';
    wrapper.innerHTML = '<span>Perfil</span><small></small>';
    wrapper.querySelector('small').textContent = role;
    label.replaceWith(wrapper);
  }

  function initGoogleLogin() {
    var form = document.getElementById('account');
    var loginButton = document.getElementById('login-submit');
    if (!form || !loginButton || form.querySelector('.orbi-google-login')) return;

    var actions = document.createElement('div');
    actions.className = 'orbi-login-actions';
    loginButton.before(actions);
    actions.appendChild(loginButton);

    var googleButton = document.createElement('button');
    googleButton.type = 'submit';
    googleButton.className = 'm3-outlined-button orbi-google-login';
    googleButton.name = 'provider';
    googleButton.value = 'Google';
    googleButton.formNoValidate = true;
    var returnUrl = new URLSearchParams(window.location.search).get('returnUrl') || '/';
    googleButton.formAction = '/Identity/Account/ExternalLogin?returnUrl=' + encodeURIComponent(returnUrl);
    googleButton.innerHTML = '<img class="orbi-google-mark" src="/assets/icons/google.png" alt="" aria-hidden="true"><span>Ingresar con Google</span>';
    actions.insertBefore(googleButton, loginButton);
  }

  function detectRole() {
    if (document.querySelector('a[href*="/Delivery/Admin"]')) return 'Administrador';
    if (document.querySelector('a[href*="/Delivery/Deliveries"]')) return 'Repartidor';
    if (document.querySelector('a[href*="/Delivery/MyOrders"]')) return 'Usuario';

    var chat = document.querySelector('[data-welcome]');
    var welcome = chat ? (chat.dataset.welcome || '').toLowerCase() : '';
    if (welcome.includes('administración')) return 'Administrador';
    if (welcome.includes('operación comercial')) return 'Vendedor';
    if (welcome.includes('tus entregas')) return 'Repartidor';
    if (welcome.includes('hacer pedidos')) return 'Usuario';
    return 'Usuario';
  }

  function initRegisterSteps() {
    var form = document.getElementById('registerForm');
    if (!form) return;

    var steps = Array.from(form.querySelectorAll('.orbi-register-step'));
    if (!steps.length) steps = buildRegisterSteps(form);
    var dots = Array.from(form.querySelectorAll('.orbi-register-progress-dot'));
    var currentStep = 0;

    function showStep(index) {
      currentStep = Math.max(0, Math.min(index, steps.length - 1));
      steps.forEach(function (step, position) {
        step.classList.toggle('active', position === currentStep);
      });
      dots.forEach(function (dot, position) {
        dot.classList.toggle('active', position === currentStep);
        dot.classList.toggle('complete', position < currentStep);
      });
    }

    function validateStep() {
      var fields = Array.from(steps[currentStep].querySelectorAll('input, select'));
      var validator = window.jQuery ? window.jQuery(form).data('validator') : null;
      var valid = true;
      fields.forEach(function (field) {
        valid = (validator ? validator.element(field) : field.checkValidity()) && valid;
      });
      if (!valid && !validator) {
        var firstInvalid = fields.find(function (field) { return !field.checkValidity(); });
        if (firstInvalid) firstInvalid.reportValidity();
      }
      return valid;
    }

    form.querySelectorAll('.orbi-register-next').forEach(function (button) {
      button.addEventListener('click', function () {
        if (validateStep()) showStep(currentStep + 1);
      });
    });
    form.querySelectorAll('.orbi-register-previous').forEach(function (button) {
      button.addEventListener('click', function () { showStep(currentStep - 1); });
    });

    var errorStep = steps.findIndex(function (step) {
      return step.querySelector('.field-validation-error');
    });
    showStep(errorStep >= 0 ? errorStep : 0);
  }

  function buildRegisterSteps(form) {
    var groups = [
      ['Input_FirstName', 'Input_LastName', 'Input_Cedula', 'Input_Role'],
      ['Input_ProvinceCode', 'Input_CityCode', 'Input_AddressLine1', 'Input_AddressLine2', 'Input_Reference'],
      ['Input_Email', 'Input_Password', 'Input_ConfirmPassword']
    ];
    var titles = ['1. Datos personales', '2. Ubicación y dirección', '3. Datos de acceso'];
    var submit = document.getElementById('registerSubmit');
    var progress = document.createElement('div');
    progress.className = 'orbi-register-progress';
    progress.setAttribute('aria-label', 'Progreso del registro');
    progress.innerHTML = groups.map(function (_, index) {
      return '<span class="orbi-register-progress-dot' + (index === 0 ? ' active' : '') + '" aria-hidden="true"></span>';
    }).join('');
    form.querySelector('.orbi-register-summary').after(progress);

    return groups.map(function (fieldIds, index) {
      var section = document.createElement('section');
      section.className = 'orbi-register-step' + (index === 0 ? ' active' : '');
      section.innerHTML = '<h2 class="orbi-register-step-title">' + titles[index] + '</h2>';
      fieldIds.forEach(function (id) {
        var field = document.getElementById(id);
        var container = field && field.closest('.m3-text-field');
        if (container) section.appendChild(container);
      });

      var actions = document.createElement('div');
      actions.className = 'orbi-register-actions';
      if (index > 0) actions.innerHTML += '<button type="button" class="m3-text-button orbi-register-previous">Anterior</button>';
      if (index < groups.length - 1) {
        actions.innerHTML += '<button type="button" class="m3-filled-button orbi-register-next">Siguiente</button>';
      } else if (submit) {
        actions.appendChild(submit);
      }
      section.appendChild(actions);
      form.appendChild(section);
      return section;
    });
  }
})();
