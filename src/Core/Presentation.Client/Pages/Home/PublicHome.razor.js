export function setMuted(video, muted) {
    if (video instanceof HTMLVideoElement) video.muted = muted;
}
