// Pilotage du lecteur video de l'espace d'apprentissage.
//
// Blazor Server ne peut pas lire l'etat d'un <video> : la position de lecture et la
// progression vivent uniquement dans le DOM. Ce module fait le pont, en remontant
// la progression au composant de facon volontairement peu bavarde (une fois toutes
// les 5 secondes de lecture, au lieu d'un appel par evenement timeupdate).

const players = new Map();

const REPORT_INTERVAL_SECONDS = 5;

export function attach(videoElement, dotNetRef, startAtSeconds) {
    if (!videoElement) {
        return;
    }

    detach(videoElement);

    const state = {
        dotNetRef,
        lastReportedAt: 0,
        completionSent: false,
        handlers: {}
    };

    // Reprise : on se positionne une seule fois, quand la duree est connue.
    state.handlers.loadedmetadata = () => {
        const target = Number(startAtSeconds) || 0;
        if (target > 0 && Number.isFinite(videoElement.duration) && target < videoElement.duration - 2) {
            try {
                videoElement.currentTime = target;
            } catch {
                // Certaines sources refusent le positionnement avant mise en cache.
            }
        }
        applyStoredPlaybackRate(videoElement);
    };

    state.handlers.timeupdate = () => {
        const duration = videoElement.duration;
        if (!Number.isFinite(duration) || duration <= 0) {
            return;
        }

        const position = videoElement.currentTime;
        const ratio = Math.min(1, position / duration);

        const crossedCompletion = ratio >= 0.9 && !state.completionSent;
        const dueForReport = position - state.lastReportedAt >= REPORT_INTERVAL_SECONDS;

        if (!crossedCompletion && !dueForReport) {
            return;
        }

        state.lastReportedAt = position;
        if (crossedCompletion) {
            state.completionSent = true;
        }

        report(state, position, ratio);
    };

    state.handlers.ended = () => {
        const duration = Number.isFinite(videoElement.duration) ? videoElement.duration : 0;
        state.completionSent = true;
        report(state, duration, 1);
    };

    // Sauvegarde a la pause et au depart de la page : sans cela, quitter en cours
    // de lecture perdait la position depuis le dernier envoi periodique.
    state.handlers.pause = () => {
        const duration = videoElement.duration;
        if (!Number.isFinite(duration) || duration <= 0) {
            return;
        }
        report(state, videoElement.currentTime, Math.min(1, videoElement.currentTime / duration));
    };

    state.handlers.ratechange = () => {
        try {
            window.localStorage.setItem('eduplatform.playbackRate', String(videoElement.playbackRate));
        } catch {
            // Mode navigation privee : le reglage n'est simplement pas memorise.
        }
    };

    for (const [event, handler] of Object.entries(state.handlers)) {
        videoElement.addEventListener(event, handler);
    }

    players.set(videoElement, state);
}

export function detach(videoElement) {
    const state = players.get(videoElement);
    if (!state) {
        return;
    }

    for (const [event, handler] of Object.entries(state.handlers)) {
        videoElement.removeEventListener(event, handler);
    }
    players.delete(videoElement);
}

export function setPlaybackRate(videoElement, rate) {
    if (!videoElement) {
        return;
    }
    videoElement.playbackRate = Number(rate) || 1;
}

export function skip(videoElement, seconds) {
    if (!videoElement || !Number.isFinite(videoElement.duration)) {
        return;
    }
    const target = videoElement.currentTime + Number(seconds);
    videoElement.currentTime = Math.max(0, Math.min(videoElement.duration, target));
}

export function getStoredPlaybackRate() {
    try {
        return Number(window.localStorage.getItem('eduplatform.playbackRate')) || 1;
    } catch {
        return 1;
    }
}

function applyStoredPlaybackRate(videoElement) {
    const rate = getStoredPlaybackRate();
    if (rate && rate !== 1) {
        videoElement.playbackRate = rate;
    }
}

function report(state, position, ratio) {
    state.dotNetRef
        .invokeMethodAsync('OnPlaybackProgress', Math.floor(position), ratio)
        .catch(() => {
            // Circuit Blazor ferme : on cesse d'emettre pour ce lecteur.
            state.dotNetRef = null;
        });
}
