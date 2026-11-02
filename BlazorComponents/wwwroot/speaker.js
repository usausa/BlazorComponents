const synthesis = window.speechSynthesis;
const jobs = new Set();
const unlockEvents = ["pointerup", "click", "keydown"];

const ready = new Promise((resolve) => {
    if (!synthesis || (synthesis.getVoices().length > 0)) {
        resolve();
        return;
    }
    synthesis.addEventListener("voiceschanged", () => resolve(), { once: true });
    setTimeout(resolve, 1000);
});

const normalize = (lang) => lang.replace("_", "-").toLowerCase();

const selectVoice = (name, lang) => {
    const voices = synthesis.getVoices();
    const named = voices.find((v) => v.name === name);
    if (named) {
        return named;
    }
    if (!lang) {
        return null;
    }
    const prefix = normalize(lang);
    const candidates = voices.filter((v) => normalize(v.lang).startsWith(prefix));
    return candidates.find((v) => v.localService) ?? candidates[0] ?? null;
};

const split = (text) => (text.match(/[^。！？!?\n]+[。！？!?\n]*/g) ?? []).filter((part) => part.trim().length > 0);

const settle = (job, result) => {
    if (jobs.delete(job)) {
        job.owner.delete(job);
        job.resolve(result);
    }
};

const cancel = () => {
    if (!synthesis) {
        return;
    }
    synthesis.cancel();
    for (const job of [...jobs]) {
        settle(job, false);
    }
};

const speak = async (owner, text, lang, voice, rate, pitch, volume) => {
    if (!synthesis) {
        return false;
    }
    let resolve;
    const result = new Promise((r) => {
        resolve = r;
    });
    const job = { owner, resolve, utterances: [] };
    jobs.add(job);
    owner.add(job);
    await ready;
    if (!jobs.has(job)) {
        return result;
    }
    const parts = split(text);
    if (parts.length === 0) {
        settle(job, true);
        return result;
    }
    const selected = selectVoice(voice, lang);
    let remaining = parts.length;
    for (const part of parts) {
        const utterance = new SpeechSynthesisUtterance(part);
        if (lang) {
            utterance.lang = lang;
        }
        if (selected) {
            utterance.voice = selected;
        }
        utterance.rate = rate;
        utterance.pitch = pitch;
        utterance.volume = volume;
        utterance.addEventListener("end", () => {
            remaining--;
            if (remaining === 0) {
                settle(job, true);
            }
        });
        utterance.addEventListener("error", () => settle(job, false));
        job.utterances.push(utterance);
        synthesis.speak(utterance);
    }
    return result;
};

const voices = async () => {
    if (!synthesis) {
        return [];
    }
    await ready;
    return synthesis.getVoices().map((v) => ({ name: v.name, lang: v.lang, localService: v.localService, default: v.default }));
};

export function create(dotNetRef) {
    const owner = new Set();
    const sounds = new Set();
    const audio = window.AudioContext ? new AudioContext() : null;
    let unlocked = false;
    let disposed = false;

    const isReady = () => (audio !== null) && (audio.state === "running");

    const notify = () => {
        if (!disposed) {
            dotNetRef.invokeMethodAsync("NotifyReady", isReady());
        }
    };

    const unlock = () => {
        if (synthesis && !unlocked) {
            unlocked = true;
            const utterance = new SpeechSynthesisUtterance("");
            utterance.volume = 0;
            synthesis.speak(utterance);
        }
        if ((audio !== null) && (audio.state !== "running") && (audio.state !== "closed")) {
            audio.resume().catch(() => { });
        }
    };

    const stopSounds = () => {
        for (const sound of [...sounds]) {
            sounds.delete(sound);
            for (const oscillator of sound.oscillators) {
                oscillator.onended = null;
                oscillator.stop();
            }
            sound.resolve(false);
        }
    };

    const play = (tones) => {
        if (!isReady()) {
            return Promise.resolve(false);
        }
        if (tones.length === 0) {
            return Promise.resolve(true);
        }
        return new Promise((resolve) => {
            const sound = { oscillators: [], resolve };
            let time = audio.currentTime;
            for (const [frequency, duration, delay] of tones) {
                const start = time + (delay / 1000);
                const end = start + (duration / 1000);
                const oscillator = audio.createOscillator();
                const gain = audio.createGain();
                oscillator.type = "sine";
                oscillator.frequency.value = frequency;
                gain.gain.setValueAtTime(0.0001, start);
                gain.gain.exponentialRampToValueAtTime(0.5, start + Math.min(0.02, (end - start) / 2));
                gain.gain.exponentialRampToValueAtTime(0.0001, end);
                oscillator.connect(gain).connect(audio.destination);
                oscillator.start(start);
                oscillator.stop(end);
                sound.oscillators.push(oscillator);
                time = end;
            }
            sound.oscillators[sound.oscillators.length - 1].onended = () => {
                if (sounds.delete(sound)) {
                    resolve(true);
                }
            };
            sounds.add(sound);
        });
    };

    for (const type of unlockEvents) {
        document.addEventListener(type, unlock, true);
    }
    audio?.addEventListener("statechange", notify);

    return {
        ready: isReady,
        play,
        speak: (...args) => speak(owner, ...args),
        cancel: () => {
            cancel();
            stopSounds();
        },
        voices,
        dispose: () => {
            disposed = true;
            for (const type of unlockEvents) {
                document.removeEventListener(type, unlock, true);
            }
            if (owner.size > 0) {
                cancel();
            }
            stopSounds();
            audio?.close().catch(() => { });
        }
    };
}
