let form;
let provinceSelect;
let citySelect;
let steps = [];
let indicators = [];
let currentStep = 0;
let cities = [];

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

function onSubmit(event) {
    if (event.submitter?.formNoValidate) return;

    const fields = Array.from(form.querySelectorAll('input:not([type="hidden"]), select'));
    const invalid = fields.find(field => !field.checkValidity());
    if (!invalid) return;

    event.preventDefault();
    const stepIndex = steps.findIndex(step => step.contains(invalid));
    if (stepIndex >= 0) showStep(stepIndex);
    invalid.reportValidity();
}

function showStep(index) {
    currentStep = Math.max(0, Math.min(index, steps.length - 1));
    steps.forEach((step, position) => step.classList.toggle('active', position === currentStep));
    indicators.forEach((indicator, position) => {
        indicator.classList.toggle('active', position === currentStep);
        indicator.classList.toggle('complete', position < currentStep);
    });
}
