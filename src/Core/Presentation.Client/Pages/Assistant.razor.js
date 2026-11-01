let activeRequest = null;
let generation = 0;
let listeners = null;

export function initialize() {
    const panel = document.getElementById('orbi-chat-panel');
    const form = document.getElementById('orbi-chat-form');
    if (!panel || !form || listeners) return;

    const input = document.getElementById('orbi-chat-input');
    const messages = document.getElementById('orbi-chat-messages');
    const suggestions = panel.querySelector('.orbi-chat-suggestions');
    const send = document.getElementById('orbi-chat-send');
    const sendArrow = send.querySelector('.orbi-chat-send-arrow');
    const sendStop = send.querySelector('.orbi-chat-send-stop');
    const newChat = document.getElementById('orbi-chat-new');

    const resizeInput = () => {
        input.style.height = 'auto';
        input.style.height = `${Math.min(input.scrollHeight, 112)}px`;
    };

    const setBusy = busy => {
        send.type = busy ? 'button' : 'submit';
        send.setAttribute('aria-label', busy ? 'Detener respuesta' : 'Enviar mensaje');
        send.title = busy ? 'Detener respuesta' : 'Enviar';
        sendArrow.hidden = busy;
        sendStop.hidden = !busy;
        send.setAttribute('aria-busy', String(busy));
    };

    const addMessage = (text, role) => {
        document.getElementById('orbi-chat-empty-state')?.remove();
        const message = document.createElement('div');
        message.className = `orbi-chat-message ${role === 'user' ? 'orbi-chat-message-user' : 'orbi-chat-message-assistant'}`;
        message.dataset.role = role;
        message.textContent = text;
        messages.appendChild(message);
        messages.scrollTop = messages.scrollHeight;
        return message;
    };

    const processEvent = (line, answerElement) => {
        if (!line.trim()) return;
        let event;
        try { event = JSON.parse(line); } catch { return; }
        if (event.type === 'delta') {
            answerElement.textContent += event.content ?? '';
        } else if (event.type === 'final') {
            answerElement.textContent = event.content ?? answerElement.textContent;
        } else if (event.type === 'status') {
            answerElement.dataset.status = event.content ?? '';
            if (!answerElement.textContent) answerElement.textContent = event.content ?? 'Generando respuesta…';
        } else if (event.type === 'error') {
            answerElement.textContent = event.content ?? 'No se pudo completar la consulta.';
            answerElement.classList.add('orbi-chat-error-message');
        }
        messages.scrollTop = messages.scrollHeight;
    };

    const handleSubmit = async event => {
        event.preventDefault();
        if (activeRequest) return;
        const text = input.value.trim();
        if (text.length < 2 || text.length > 500) return;

        suggestions.hidden = true;
        addMessage(text, 'user');
        input.value = '';
        resizeInput();

        const answer = addMessage('Conectando con el asistente…', 'assistant');
        answer.classList.add('orbi-chat-typing');
        const requestId = ++generation;
        const controller = new AbortController();
        activeRequest = controller;
        let timedOut = false;
        const timeoutId = window.setTimeout(() => {
            timedOut = true;
            controller.abort('timeout');
        }, Number(panel.dataset.timeoutMs) || 120000);
        setBusy(true);

        try {
            const tokenResponse = await fetch('/api/security/antiforgery', {
                method: 'GET',
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { 'Accept': 'application/json' },
                signal: controller.signal
            });
            if (!tokenResponse.ok) throw new Error(`HTTP_${tokenResponse.status}`);
            const { requestToken } = await tokenResponse.json();

            const response = await fetch('/Home/ProductChat', {
                method: 'POST',
                credentials: 'same-origin',
                cache: 'no-store',
                headers: {
                    'Accept': 'application/x-ndjson',
                    'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8',
                    'RequestVerificationToken': requestToken
                },
                body: new URLSearchParams({ message: text, context: panel.dataset.context ?? 'public_home' }),
                signal: controller.signal
            });

            if (!response.ok) {
                const message = response.status === 401 ? 'Inicia sesión para continuar.'
                    : response.status === 403 ? 'Tu cuenta no tiene permiso para esta acción.'
                    : response.status === 404 ? 'No encontramos el servicio del asistente.'
                    : response.status === 408 || response.status === 504 ? 'La solicitud agotó el tiempo de espera.'
                    : response.status >= 500 ? 'El servicio no está disponible por el momento.'
                    : 'No se pudo enviar la consulta.';
                throw new Error(`HTTP_${response.status}:${message}`);
            }
            if (!response.body) throw new TypeError('El navegador no admite respuestas progresivas.');

            answer.classList.remove('orbi-chat-typing');
            answer.textContent = '';
            const reader = response.body.getReader();
            const decoder = new TextDecoder();
            let buffer = '';
            while (true) {
                const { value, done } = await reader.read();
                if (done) break;
                buffer += decoder.decode(value, { stream: true });
                const lines = buffer.split('\n');
                buffer = lines.pop() ?? '';
                for (const line of lines) processEvent(line, answer);
                if (requestId !== generation) return;
            }
            buffer += decoder.decode();
            processEvent(buffer, answer);
        } catch (error) {
            if (requestId !== generation) return;
            answer.classList.remove('orbi-chat-typing');
            if (error?.name === 'AbortError') {
                answer.textContent = timedOut ? 'El asistente superó el tiempo máximo. Puedes intentarlo otra vez.' : 'Respuesta detenida.';
            } else if (error instanceof TypeError) {
                answer.textContent = 'No se pudo conectar con el asistente. Revisa tu conexión e inténtalo otra vez.';
            } else if (String(error?.message).startsWith('HTTP_')) {
                answer.textContent = String(error.message).split(':').slice(1).join(':') || 'No se pudo completar la solicitud.';
            } else {
                answer.textContent = 'Ocurrió un error. Inténtalo otra vez.';
            }
            answer.classList.add('orbi-chat-error-message');
        } finally {
            window.clearTimeout(timeoutId);
            if (requestId === generation) {
                activeRequest = null;
                setBusy(false);
                messages.scrollTop = messages.scrollHeight;
                input.focus();
            }
        }
    };

    const onSuggestionClick = event => {
        const button = event.target.closest('.orbi-chat-suggestion');
        if (!button || activeRequest) return;
        input.value = button.dataset.question ?? '';
        resizeInput();
        form.requestSubmit();
    };

    const onSendClick = () => {
        if (activeRequest) activeRequest.abort('user');
    };

    const onInput = () => resizeInput();
    const onKeyDown = event => {
        if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            if (!activeRequest) form.requestSubmit();
        }
    };

    const onNewChat = () => {
        generation++;
        activeRequest?.abort('new-chat');
        activeRequest = null;
        messages.replaceChildren();
        const welcome = document.createElement('div');
        welcome.className = 'orbi-chat-empty-state';
        welcome.id = 'orbi-chat-empty-state';
        welcome.innerHTML = '<span class="material-symbols-outlined" aria-hidden="true">auto_awesome</span><h1>¿En qué te ayudo hoy?</h1><p></p>';
        welcome.querySelector('p').textContent = panel.dataset.welcome ?? '';
        messages.appendChild(welcome);
        suggestions.hidden = false;
        input.value = '';
        resizeInput();
        setBusy(false);
        input.focus();
    };

    form.addEventListener('submit', handleSubmit);
    suggestions.addEventListener('click', onSuggestionClick);
    send.addEventListener('click', onSendClick);
    newChat.addEventListener('click', onNewChat);
    input.addEventListener('input', onInput);
    input.addEventListener('keydown', onKeyDown);
    resizeInput();
    listeners = { form, suggestions, send, newChat, input, handleSubmit, onSuggestionClick, onSendClick, onNewChat, onInput, onKeyDown };
}

export function dispose() {
    generation++;
    activeRequest?.abort('dispose');
    activeRequest = null;
    if (!listeners) return;
    listeners.form.removeEventListener('submit', listeners.handleSubmit);
    listeners.suggestions.removeEventListener('click', listeners.onSuggestionClick);
    listeners.send.removeEventListener('click', listeners.onSendClick);
    listeners.newChat.removeEventListener('click', listeners.onNewChat);
    listeners.input.removeEventListener('input', listeners.onInput);
    listeners.input.removeEventListener('keydown', listeners.onKeyDown);
    listeners = null;
}
