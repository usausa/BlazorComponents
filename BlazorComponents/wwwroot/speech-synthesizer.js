const synthesis = window.speechSynthesis;
const jobs = new Set();

const ready = new Promise((resolve) => {
    if (!synthesis || (synthesis.getVoices().length > 0)) {
        resolve();
        return;
    }
    synthesis.addEventListener("voiceschanged", () => resolve(), { once: true });
    setTimeout(resolve, 1000);
});

if (synthesis) {
    const unlock = () => {
        document.removeEventListener("click", unlock, true);
        const utterance = new SpeechSynthesisUtterance("");
        utterance.volume = 0;
        synthesis.speak(utterance);
    };
    document.addEventListener("click", unlock, true);
}

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

export function create() {
    const owner = new Set();

    return {
        speak: (...args) => speak(owner, ...args),
        cancel,
        voices,
        dispose: () => {
            if (owner.size > 0) {
                cancel();
            }
        }
    };
}
