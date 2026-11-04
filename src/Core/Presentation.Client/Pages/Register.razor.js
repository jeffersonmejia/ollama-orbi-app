let form;
let provinceSelect;
let citySelect;
let steps = [];
let indicators = [];
let currentStep = 0;
let cities = [];
let isSubmitting = false;

export async function loadOptions() {
    const response = await fetch('/api/identity/registration/options', {
        method: 'GET',
        credentials: 'same-origin',
        headers: { Accept: 'application/json' },
        cache: 'no-store'
    });

    if (!response.ok) throw new Error(`Catalog request failed (${response.status})`);
    return await response.json();
}

export function initializeWizard() {
    form = document.getElementById('registerForm');
    if (!form) return;
    isSubmitting = false;

    provinceSelect = form.querySelector('#Input_ProvinceCode');
    citySelect = form.querySelector('#Input_CityCode');
    steps = Array.from(form.querySelectorAll('.orbi-register-step'));
    indicators = Array.from(form.querySelectorAll('.orbi-register-progress span'));
    form.querySelectorAll('.orbi-register-next, button[type="submit"]').forEach(button => button.disabled = true);

    form.addEventListener('click', onWizardClick);
    form.addEventListener('submit', onSubmit);
    provinceSelect?.addEventListener('change', () => populateCities(''));
}

export function populateOptions(options) {
    if (!form) return;

    const token = form.querySelector('input[name="__RequestVerificationToken"]');
    if (token) token.value = options.requestToken ?? '';

    const roleSelect = form.querySelector('#Input_Role');
    roleSelect.replaceChildren(...options.roles.map(role => new Option(role, role)));
    if (options.roles.includes('Usuario')) roleSelect.value = 'Usuario';
    roleSelect.disabled = false;

    replaceOptions(provinceSelect, 'Selecciona una provincia', options.provinces.map(item => [item.code, item.name]));
    provinceSelect.disabled = false;

    cities = options.cities ?? [];
    replaceOptions(citySelect, 'Selecciona primero una provincia', []);
    citySelect.disabled = true;

    form.querySelectorAll('.orbi-register-next, button[type="submit"]').forEach(button => button.disabled = false);
}

function replaceOptions(select, placeholder, entries) {
    if (!select) return;
    select.replaceChildren(new Option(placeholder, ''));
    for (const [value, text] of entries) select.add(new Option(text, value));
}

function populateCities(selectedCode) {
    const filtered = cities.filter(city => city.provinceCode === provinceSelect?.value);
    replaceOptions(citySelect, filtered.length ? 'Selecciona una ciudad' : 'Selecciona primero una provincia',
        filtered.map(city => [city.code, city.name]));
    citySelect.disabled = filtered.length === 0;
    if (selectedCode) citySelect.value = selectedCode;
}

function onWizardClick(event) {
    const next = event.target.closest('.orbi-register-next');
    const previous = event.target.closest('.orbi-register-previous');
    if (!next && !previous) return;

    event.preventDefault();
    if (next) {
        const fields = Array.from(steps[currentStep].querySelectorAll('input,select'));
        const invalid = fields.find(field => !field.checkValidity());
        if (invalid) {
            invalid.reportValidity();
            return;
        }
        showStep(currentStep + 1);
    } else {
        showStep(currentStep - 1);
    }
}

async function onSubmit(event) {
    if (isSubmitting) {
        event.preventDefault();
        return;
    }

    const isGoogleSignUp = event.submitter?.formNoValidate === true;
    if (!isGoogleSignUp) {
        const fields = Array.from(form.querySelectorAll('input:not([type="hidden"]), select'));
        const invalid = fields.find(field => !field.checkValidity());
        if (invalid) {
            event.preventDefault();
            const stepIndex = steps.findIndex(step => step.contains(invalid));
            if (stepIndex >= 0) showStep(stepIndex);
            invalid.reportValidity();
            return;
        }
    }

    isSubmitting = true;
    const submitter = event.submitter ?? form.querySelector('.orbi-register-actions button[type="submit"]');
    if (!submitter) return;

    const idleMarkup = submitter.innerHTML;
    const spinner = document.createElement('span');
    spinner.className = 'orbi-register-submit-spinner';
    spinner.setAttribute('aria-hidden', 'true');
    submitter.replaceChildren(spinner, document.createTextNode(isGoogleSignUp ? 'Conectando con Google…' : 'Creando cuenta…'));
    submitter.setAttribute('aria-busy', 'true');

    form.querySelectorAll('button[type="submit"]').forEach(button => {
        if (button !== submitter) button.disabled = true;
    });

    if (!isGoogleSignUp) {
        event.preventDefault();
        await submitRegistration(submitter, idleMarkup);
    }
}

async function submitRegistration(submitter, idleMarkup) {
    const message = document.getElementById('registerSubmitMessage');
    if (message) message.hidden = true;

    try {
        const response = await fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            credentials: 'same-origin',
            redirect: 'follow'
        });
        const responseUrl = new URL(response.url, window.location.href);
        const registrationEndpoint = new URL(form.action, window.location.href);

        if (responseUrl.pathname.toLowerCase() === registrationEndpoint.pathname.toLowerCase()) {
            const html = await response.text();
            const responseDocument = new DOMParser().parseFromString(html, 'text/html');
            const serverMessage = responseDocument.querySelector('.validation-summary-errors li, .field-validation-error')?.textContent?.trim();
            showSubmitMessage(message, serverMessage || 'No se pudo crear la cuenta. Revisa los datos e inténtalo nuevamente.');
            restoreSubmitState(submitter, idleMarkup);
            return;
        }

        window.location.assign(responseUrl.href);
    } catch {
        showSubmitMessage(message, 'No se pudo completar el registro. Revisa tu conexión e inténtalo nuevamente.');
        restoreSubmitState(submitter, idleMarkup);
    }
}

function showSubmitMessage(message, text) {
    if (!message) return;
    message.textContent = text;
    message.hidden = false;
}

function restoreSubmitState(submitter, idleMarkup) {
    isSubmitting = false;
    submitter.innerHTML = idleMarkup;
    submitter.removeAttribute('aria-busy');
    form.querySelectorAll('button[type="submit"]').forEach(button => button.disabled = false);
}

function showStep(index) {
    currentStep = Math.max(0, Math.min(index, steps.length - 1));
    steps.forEach((step, position) => step.classList.toggle('active', position === currentStep));
    indicators.forEach((indicator, position) => {
        indicator.classList.toggle('active', position === currentStep);
        indicator.classList.toggle('complete', position < currentStep);
    });
}
