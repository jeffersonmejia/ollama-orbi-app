(function () {
  'use strict';

  window.createOrbiVoiceController = function (options) {
    const form = options.form;
    const input = options.input;
    const micButton = options.micButton;
    const micIcon = micButton.querySelector('.material-symbols-outlined');
    const modeButton = options.modeButton;
    const modeIcon = modeButton.querySelector('.material-symbols-outlined');
    const supported = !!(navigator.mediaDevices?.getUserMedia && window.MediaRecorder && (window.AudioContext || window.webkitAudioContext));
    let recorder = null;
    let stream = null;
    let audioContext = null;
    let analyser = null;
    let animationFrame = null;
    let maxTimer = null;
    let chunks = [];
    let modeActive = false;
    let automaticTurn = false;
    let heardSpeech = false;
    let silenceStarted = 0;
    let playback = null;
    let playbackUrl = null;
    let discardRecording = false;

    if (!supported) {
      micButton.hidden = true;
      modeButton.hidden = true;
      return { stop: function () {}, playResponse: function () {}, get isModeActive() { return false; } };
    }

    function csrfToken() {
      return form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    function resetButtons() {
      micButton.classList.remove('listening');
      modeButton.classList.remove('listening');
      micIcon.textContent = 'mic';
      modeIcon.textContent = 'headset_mic';
      micButton.setAttribute('aria-label', 'Dictar mensaje');
      micButton.title = 'Dictar mensaje';
    }

    function updateModeButton() {
      modeButton.classList.toggle('active', modeActive);
      modeButton.setAttribute('aria-pressed', String(modeActive));
      modeButton.setAttribute('aria-label', modeActive ? 'Detener modo voz' : 'Iniciar modo voz');
      modeButton.title = modeActive ? 'Detener modo voz' : 'Modo voz';
      if (!recorder || recorder.state !== 'recording') modeIcon.textContent = 'headset_mic';
    }

    function releaseCapture() {
      if (animationFrame) cancelAnimationFrame(animationFrame);
      if (maxTimer) clearTimeout(maxTimer);
      animationFrame = null;
      maxTimer = null;
      stream?.getTracks().forEach(track => track.stop());
      stream = null;
      audioContext?.close().catch(() => {});
      audioContext = null;
      analyser = null;
      resetButtons();
      updateModeButton();
    }

    function stopRecording() {
      if (recorder?.state === 'recording') recorder.stop();
    }

    function monitorSilence() {
      if (!analyser || recorder?.state !== 'recording') return;
      const values = new Uint8Array(analyser.fftSize);
      analyser.getByteTimeDomainData(values);
      let sum = 0;
      for (const value of values) {
        const sample = (value - 128) / 128;
        sum += sample * sample;
      }
      const volume = Math.sqrt(sum / values.length);
      const now = Date.now();
      if (volume > 0.025) {
        heardSpeech = true;
        silenceStarted = 0;
      } else if (heardSpeech) {
        silenceStarted ||= now;
        if (now - silenceStarted > 1000) {
          stopRecording();
          return;
        }
      }
      animationFrame = requestAnimationFrame(monitorSilence);
    }

    async function transcribe(blob, autoSubmit) {
      if (blob.size < 500) {
        if (modeActive) setTimeout(() => startRecording(true), 350);
        return;
      }
      const data = new FormData();
      const extension = blob.type.includes('mp4') ? 'm4a' : 'webm';
      data.append('audio', blob, 'voice.' + extension);
      data.append('__RequestVerificationToken', csrfToken());
      const targetIcon = autoSubmit ? modeIcon : micIcon;
      targetIcon.textContent = 'hourglass_top';
      try {
        const response = await fetch('/Home/TranscribeAudio', { method: 'POST', body: data });
        const result = await response.json();
        if (!response.ok) throw new Error(result.message || 'No se pudo transcribir el audio.');
        input.value = (result.text || '').trim();
        options.resize();
        if (autoSubmit && modeActive && input.value.length >= 2) form.requestSubmit();
        else if (modeActive) setTimeout(() => startRecording(true), 350);
        else input.focus();
      } catch (error) {
        options.onError?.(error.message || 'No se pudo transcribir el audio.');
        if (modeActive) setTimeout(() => startRecording(true), 700);
      } finally {
        resetButtons();
        updateModeButton();
      }
    }

    async function startRecording(autoSubmit) {
      if (options.isBusy() || recorder?.state === 'recording' || playback) return;
      automaticTurn = autoSubmit;
      chunks = [];
      heardSpeech = false;
      silenceStarted = 0;
      try {
        stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true } });
        const preferredType = MediaRecorder.isTypeSupported('audio/webm;codecs=opus') ? 'audio/webm;codecs=opus' : '';
        recorder = preferredType ? new MediaRecorder(stream, { mimeType: preferredType }) : new MediaRecorder(stream);
        recorder.addEventListener('dataavailable', event => { if (event.data.size) chunks.push(event.data); });
        recorder.addEventListener('stop', () => {
          const blob = new Blob(chunks, { type: recorder.mimeType || 'audio/webm' });
          const shouldSubmit = automaticTurn;
          const shouldDiscard = discardRecording;
          discardRecording = false;
          releaseCapture();
          if (!shouldDiscard) transcribe(blob, shouldSubmit);
        }, { once: true });
        const AudioContextClass = window.AudioContext || window.webkitAudioContext;
        audioContext = new AudioContextClass();
        analyser = audioContext.createAnalyser();
        analyser.fftSize = 1024;
        audioContext.createMediaStreamSource(stream).connect(analyser);
        recorder.start(250);
        if (autoSubmit) {
          modeButton.classList.add('listening');
          modeIcon.textContent = 'graphic_eq';
        } else {
          micButton.classList.add('listening');
          micIcon.textContent = 'graphic_eq';
          micButton.setAttribute('aria-label', 'Detener grabación');
          micButton.title = 'Detener grabación';
        }
        maxTimer = setTimeout(stopRecording, 15000);
        monitorSilence();
      } catch (error) {
        releaseCapture();
        if (error.name === 'NotAllowedError') setMode(false);
        options.onError?.('No se pudo acceder al micrófono. Revisa el permiso del navegador.');
      }
    }

    function stopPlayback() {
      if (playback) {
        playback.pause();
        playback.src = '';
      }
      playback = null;
      if (playbackUrl) URL.revokeObjectURL(playbackUrl);
      playbackUrl = null;
    }

    function setMode(enabled) {
      modeActive = enabled;
      if (!enabled) {
        discardRecording = recorder?.state === 'recording';
        stopRecording();
        stopPlayback();
      } else {
        input.value = '';
        options.resize();
        startRecording(true);
      }
      updateModeButton();
    }

    async function playResponse(text) {
      if (!modeActive || !text) return;
      modeIcon.textContent = 'hourglass_top';
      try {
        const response = await fetch('/Home/SynthesizeSpeech', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken() },
          body: JSON.stringify({ text: text })
        });
        if (!response.ok) throw new Error('No se pudo generar la voz de Orbi.');
        playbackUrl = URL.createObjectURL(await response.blob());
        playback = new Audio(playbackUrl);
        modeIcon.textContent = 'spatial_audio';
        const continueConversation = () => {
          stopPlayback();
          updateModeButton();
          if (modeActive) setTimeout(() => startRecording(true), 350);
        };
        playback.addEventListener('ended', continueConversation, { once: true });
        playback.addEventListener('error', continueConversation, { once: true });
        await playback.play();
      } catch (error) {
        stopPlayback();
        updateModeButton();
        options.onError?.(error.message || 'No se pudo reproducir la voz de Orbi.');
        if (modeActive) setTimeout(() => startRecording(true), 700);
      }
    }

    micButton.addEventListener('click', () => {
      if (modeActive) setMode(false);
      if (recorder?.state === 'recording') stopRecording();
      else startRecording(false);
    });
    modeButton.addEventListener('click', () => setMode(!modeActive));

    return {
      stop: () => setMode(false),
      playResponse,
      get isModeActive() { return modeActive; }
    };
  };
})();
