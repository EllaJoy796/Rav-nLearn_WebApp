/* ═══════════════════════════════════════════════════════════════
   STUDY BUDDY — Drop-in for QuizSet.cshtml
   Voice is configured once in Profile > Study Buddy settings.
   ═══════════════════════════════════════════════════════════════ */

const GEMINI_API_KEY = 'AIzaSyC1DHJFbCXy8kLZQ6_F0B7-pIk-JMY8k44';
const GEMINI_URL = `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key=${GEMINI_API_KEY}`;

/* ══════════════════════════════════════
   INJECT STYLES
══════════════════════════════════════ */
(function injectStyles() {
    const style = document.createElement('style');
    style.textContent = `
    #sbOverlay {
        position: fixed; inset: 0; z-index: 1100;
        background: rgba(0,0,0,0.65);
        backdrop-filter: blur(6px);
        display: none; align-items: center; justify-content: center;
    }
    #sbOverlay.open { display: flex; }

    #sbModal {
        background: var(--card-bg);
        border: 1px solid var(--border);
        border-radius: 24px;
        width: min(560px, 94vw);
        max-height: 90vh;
        overflow: hidden;
        box-shadow: 0 32px 80px rgba(0,0,0,0.5);
        animation: sbIn .25s cubic-bezier(.16,1,.3,1);
        display: flex; flex-direction: column;
    }
    @keyframes sbIn {
        from { opacity:0; transform: scale(.93) translateY(20px); }
        to   { opacity:1; transform: none; }
    }

    #sbHeader {
        display: flex; align-items: center; justify-content: space-between;
        padding: 20px 24px 16px;
        border-bottom: 1px solid var(--border);
        flex-shrink: 0;
    }
    #sbHeaderLeft { display: flex; flex-direction: column; gap: 3px; }
    #sbTitle {
        font-family: 'Syne', sans-serif;
        font-size: 15px; font-weight: 800;
        color: var(--text-white); letter-spacing: .2px;
    }
    #sbSubtitle { font-size: 12px; color: var(--text-muted); }
    #sbCloseBtn {
        background: none; border: 1px solid var(--border);
        border-radius: 8px; color: var(--text-dim);
        width: 30px; height: 30px; cursor: pointer;
        display: flex; align-items: center; justify-content: center;
        font-size: 16px; line-height: 1;
        transition: background .2s, color .2s;
    }
    #sbCloseBtn:hover { background: var(--accent-soft); color: var(--text-white); }

    #sbTabs {
        display: flex; gap: 0;
        border-bottom: 1px solid var(--border);
        padding: 0 24px;
        flex-shrink: 0;
    }
    .sb-tab {
        font-size: 12px; font-weight: 700; letter-spacing: .4px;
        padding: 10px 16px; border-bottom: 2px solid transparent;
        color: var(--text-dim); cursor: default;
        display: flex; align-items: center; gap: 7px;
        transition: color .2s, border-color .2s;
        user-select: none;
    }
    .sb-tab.active { color: var(--accent); border-bottom-color: var(--accent); }
    .sb-tab svg { width: 14px; height: 14px; stroke: currentColor; fill: none; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }

    #sbProgressBar { height: 3px; background: var(--accent-soft); flex-shrink: 0; }
    #sbProgressFill {
        height: 100%; background: var(--accent);
        border-radius: 2px; transition: width .4s ease; width: 0%;
    }

    /* ── STAGE ── */
    #sbStage {
        display: flex; flex-direction: column;
        align-items: center; justify-content: center;
        padding: 36px 28px 28px; gap: 20px;
        min-height: 280px;
    }

    #sbOrb {
        width: 88px; height: 88px; border-radius: 50%;
        background: var(--accent-soft);
        border: 2px solid rgba(124,92,191,.3);
        display: flex; align-items: center; justify-content: center;
        position: relative; transition: border-color .3s;
    }
    #sbOrb.speaking { border-color: var(--accent); animation: orbPulse 1.8s ease-in-out infinite; }
    #sbOrb.listening { border-color: #5dbf8a; animation: orbListenPulse 1.2s ease-in-out infinite; }
    @keyframes orbPulse {
        0%,100% { box-shadow: 0 0 0 0 rgba(124,92,191,.3); }
        50%      { box-shadow: 0 0 0 14px rgba(124,92,191,.0); }
    }
    @keyframes orbListenPulse {
        0%,100% { box-shadow: 0 0 0 0 rgba(93,191,138,.3); }
        50%      { box-shadow: 0 0 0 14px rgba(93,191,138,.0); }
    }
    #sbOrb svg { width: 36px; height: 36px; stroke: var(--accent); fill: none; stroke-width: 1.6; stroke-linecap: round; stroke-linejoin: round; }
    #sbOrb.listening svg { stroke: #5dbf8a; }

    #sbWave {
        display: flex; align-items: center; gap: 3px; height: 28px;
        opacity: 0; transition: opacity .3s;
    }
    #sbWave.active { opacity: 1; }
    .sb-bar {
        width: 3px; border-radius: 2px; background: var(--accent);
        animation: waveAnim 1s ease-in-out infinite;
    }
    #sbWave.listening .sb-bar { background: #5dbf8a; }
    .sb-bar:nth-child(1) { animation-delay: 0s;    height: 10px; }
    .sb-bar:nth-child(2) { animation-delay: .1s;   height: 20px; }
    .sb-bar:nth-child(3) { animation-delay: .2s;   height: 28px; }
    .sb-bar:nth-child(4) { animation-delay: .3s;   height: 18px; }
    .sb-bar:nth-child(5) { animation-delay: .4s;   height: 24px; }
    .sb-bar:nth-child(6) { animation-delay: .35s;  height: 14px; }
    .sb-bar:nth-child(7) { animation-delay: .15s;  height: 22px; }
    @keyframes waveAnim {
        0%,100% { transform: scaleY(.4); opacity:.5; }
        50%     { transform: scaleY(1);  opacity:1;  }
    }

    #sbStateLabel {
        font-size: 11px; font-weight: 700; letter-spacing: 1.2px;
        text-transform: uppercase; color: var(--text-dim);
        transition: color .3s;
    }
    #sbStateLabel.speaking  { color: var(--accent); }
    #sbStateLabel.listening { color: #5dbf8a; }
    #sbStateLabel.thinking  { color: #bf9c5d; }

    #sbQuestion {
        background: rgba(124,92,191,.08);
        border: 1px solid rgba(124,92,191,.18);
        border-radius: 14px; padding: 14px 18px;
        font-size: 13px; line-height: 1.65;
        color: var(--text-white); width: 100%;
        min-height: 56px; text-align: center;
    }
    #sbQuestion .q-num {
        font-size: 11px; color: var(--accent); font-weight: 700;
        letter-spacing: .5px; margin-bottom: 6px;
    }

    #sbFeedback {
        width: 100%; border-radius: 12px; padding: 12px 16px;
        font-size: 13px; line-height: 1.6; color: var(--text-white);
        display: none; animation: feedIn .3s ease;
    }
    @keyframes feedIn { from { opacity:0; transform:translateY(6px); } to { opacity:1; transform:none; } }
    #sbFeedback.correct { background: rgba(46,139,87,.12); border: 1px solid rgba(46,139,87,.25); }
    #sbFeedback.wrong   { background: rgba(192,57,43,.12);  border: 1px solid rgba(192,57,43,.25); }

    #sbTranscript {
        font-size: 12px; color: var(--text-muted);
        min-height: 18px; font-style: italic; text-align: center;
    }

    #sbFooter {
        display: flex; align-items: center; justify-content: space-between;
        padding: 14px 24px 20px;
        border-top: 1px solid var(--border);
        gap: 10px; flex-shrink: 0;
    }
    #sbScore { font-size: 12px; color: var(--text-muted); }
    #sbFooterBtns { display: flex; gap: 8px; }

    .sb-btn {
        height: 36px; border-radius: 10px;
        font-size: 12px; font-weight: 700; letter-spacing: .3px;
        cursor: pointer; font-family: inherit;
        display: flex; align-items: center; gap: 6px;
        padding: 0 16px; transition: all .2s;
    }
    .sb-btn svg { width: 13px; height: 13px; stroke: currentColor; fill: none; stroke-width: 2.2; stroke-linecap: round; stroke-linejoin: round; }
    #sbEndBtn  { background: transparent; border: 1px solid rgba(192,57,43,.35); color: #e07070; }
    #sbEndBtn:hover { background: rgba(192,57,43,.1); border-color: #e07070; }
    #sbSkipBtn { background: transparent; border: 1px solid var(--border); color: var(--text-muted); }
    #sbSkipBtn:hover { background: var(--accent-soft); border-color: var(--accent); color: var(--text-white); }
    #sbMicBtn  { background: var(--accent-soft); border: 1px solid rgba(124,92,191,.35); color: var(--text-white); }
    #sbMicBtn:hover { background: var(--accent); }

    #sbResults {
        display: none; flex-direction: column;
        align-items: center; gap: 16px;
        padding: 36px 28px;
    }
    .sb-result-ring {
        width: 100px; height: 100px; border-radius: 50%;
        border: 3px solid var(--accent);
        display: flex; flex-direction: column;
        align-items: center; justify-content: center;
    }
    .sb-result-num  { font-family: 'Syne', sans-serif; font-size: 28px; font-weight: 900; color: var(--accent); }
    .sb-result-label { font-size: 10px; color: var(--text-muted); font-weight: 700; letter-spacing: .5px; }
    .sb-result-title { font-family: 'Syne', sans-serif; font-size: 18px; font-weight: 800; color: var(--text-white); }
    .sb-result-sub  { font-size: 13px; color: var(--text-muted); text-align: center; }
    .sb-result-btns { display: flex; gap: 10px; margin-top: 4px; }
    .sb-btn-retry {
        height: 40px; border-radius: 10px; padding: 0 20px;
        background: transparent; border: 1px solid var(--border);
        color: var(--accent); font-size: 13px; font-weight: 700;
        font-family: inherit; cursor: pointer; transition: all .2s;
    }
    .sb-btn-retry:hover { background: var(--accent-soft); border-color: var(--accent); }
    .sb-btn-done {
        height: 40px; border-radius: 10px; padding: 0 28px;
        background: var(--accent); border: none;
        color: #fff; font-size: 13px; font-weight: 700;
        font-family: inherit; cursor: pointer; transition: background .2s;
    }
    .sb-btn-done:hover { background: #6b4daa; }

    #sbFallbackRow { display: none; width: 100%; gap: 8px; align-items: center; }
    #sbFallbackInput {
        flex: 1; height: 38px; background: var(--bg);
        border: 1px solid var(--border); border-radius: 9px;
        padding: 0 12px; color: var(--text-white);
        font-size: 13px; font-family: inherit; outline: none;
    }
    #sbFallbackInput:focus { border-color: var(--accent); }
    #sbFallbackSubmit {
        height: 38px; padding: 0 16px; border-radius: 9px;
        background: var(--accent); border: none;
        color: #fff; font-size: 13px; font-weight: 700;
        font-family: inherit; cursor: pointer;
    }
    `;
    document.head.appendChild(style);
})();

/* ══════════════════════════════════════
   INJECT HTML  (no voice picker panel)
══════════════════════════════════════ */
(function injectHTML() {
    const el = document.createElement('div');
    el.id = 'sbOverlay';
    el.innerHTML = `
    <div id="sbModal">

      <!-- HEADER -->
      <div id="sbHeader">
        <div id="sbHeaderLeft">
          <div id="sbTitle">Study Buddy</div>
          <div id="sbSubtitle">Loading…</div>
        </div>
        <button id="sbCloseBtn" onclick="window.SB.close()">✕</button>
      </div>

      <!-- TABS -->
      <div id="sbTabs">
        <div class="sb-tab active" id="sbTabAI">
          <svg viewBox="0 0 24 24"><path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"/><path d="M19 10v2a7 7 0 0 1-14 0v-2"/><line x1="12" y1="19" x2="12" y2="23"/><line x1="8" y1="23" x2="16" y2="23"/></svg>
          AI Speaking
        </div>
        <div class="sb-tab" id="sbTabUser">
          <svg viewBox="0 0 24 24"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
          Your Turn
        </div>
      </div>

      <!-- PROGRESS -->
      <div id="sbProgressBar"><div id="sbProgressFill"></div></div>

      <!-- STAGE -->
      <div id="sbStage">
        <div id="sbOrb">
          <svg viewBox="0 0 24 24">
            <path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"/>
            <path d="M19 10v2a7 7 0 0 1-14 0v-2"/>
            <line x1="12" y1="19" x2="12" y2="23"/>
            <line x1="8" y1="23" x2="16" y2="23"/>
          </svg>
        </div>
        <div id="sbWave">
          <div class="sb-bar"></div><div class="sb-bar"></div><div class="sb-bar"></div>
          <div class="sb-bar"></div><div class="sb-bar"></div><div class="sb-bar"></div>
          <div class="sb-bar"></div>
        </div>
        <div id="sbStateLabel">READY</div>
        <div id="sbQuestion">
          <div class="q-num" id="sbQNum">QUESTION 1 OF 10</div>
          <div id="sbQText">Preparing your session…</div>
        </div>
        <div id="sbTranscript"></div>
        <div id="sbFallbackRow">
          <input id="sbFallbackInput" type="text" placeholder="Type your answer…" />
          <button id="sbFallbackSubmit" onclick="window.SB.submitTextAnswer()">Submit</button>
        </div>
        <div id="sbFeedback"></div>
      </div>

      <!-- RESULTS -->
      <div id="sbResults">
        <div class="sb-result-ring">
          <div class="sb-result-num" id="sbResScore">0/0</div>
          <div class="sb-result-label">SCORE</div>
        </div>
        <div class="sb-result-title" id="sbResTitle">Session complete!</div>
        <div class="sb-result-sub"  id="sbResSub">Great job reviewing this quiz.</div>
        <div class="sb-result-btns">
          <button class="sb-btn-retry" onclick="window.SB.retry()">↺ Try Again</button>
          <button class="sb-btn-done"  onclick="window.SB.close()">Done</button>
        </div>
      </div>

      <!-- FOOTER -->
      <div id="sbFooter">
        <div id="sbScore">0 / 0 correct</div>
        <div id="sbFooterBtns">
          <button class="sb-btn" id="sbEndBtn"  onclick="window.SB.close()">
            <svg viewBox="0 0 24 24"><rect x="3" y="3" width="18" height="18" rx="2"/></svg>
            End session
          </button>
          <button class="sb-btn" id="sbSkipBtn" onclick="window.SB.skip()">
            Skip
            <svg viewBox="0 0 24 24"><polyline points="9 18 15 12 9 6"/></svg>
          </button>
          <button class="sb-btn" id="sbMicBtn"  onclick="window.SB.toggleMic()">
            <svg viewBox="0 0 24 24"><path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"/><path d="M19 10v2a7 7 0 0 1-14 0v-2"/></svg>
            Listen
          </button>
        </div>
      </div>

    </div>`;
    document.body.appendChild(el);

    document.getElementById('sbFallbackInput').addEventListener('keydown', e => {
        if (e.key === 'Enter') window.SB.submitTextAnswer();
    });
})();

/* ══════════════════════════════════════
   STUDY BUDDY ENGINE
══════════════════════════════════════ */
window.SB = (() => {

    /* ── state ── */
    let questions        = [];
    let quizLabel        = '';
    let idx              = 0;
    let score            = 0;
    let answered         = 0;
    let waitingAns       = false;
    let recognition      = null;
    let synth            = window.speechSynthesis;
    let speechSupported  = false;
    let selectedVoice    = null;
    let closed           = false;

    /* ── DOM helpers ── */
    const $           = id => document.getElementById(id);
    const overlay     = () => $('sbOverlay');
    const orb         = () => $('sbOrb');
    const wave        = () => $('sbWave');
    const stateLabel  = () => $('sbStateLabel');
    const tabAI       = () => $('sbTabAI');
    const tabUser     = () => $('sbTabUser');
    const qNum        = () => $('sbQNum');
    const qText       = () => $('sbQText');
    const transcript  = () => $('sbTranscript');
    const feedback    = () => $('sbFeedback');
    const progFill    = () => $('sbProgressFill');
    const scoreEl     = () => $('sbScore');
    const stage       = () => $('sbStage');
    const results     = () => $('sbResults');
    const fallbackRow = () => $('sbFallbackRow');
    const footer      = () => $('sbFooter');

    /* ── helpers ── */
    function setTab(who) {
        tabAI().classList.toggle('active', who === 'ai');
        tabUser().classList.toggle('active', who === 'user');
    }

    function setOrb(mode) {
        orb().className = '';
        wave().className = 'active';
        stateLabel().className = mode;
        if (mode === 'speaking') {
            orb().classList.add('speaking');
            wave().classList.remove('listening');
            stateLabel().textContent = 'AI IS SPEAKING…';
            setTab('ai');
        } else if (mode === 'listening') {
            orb().classList.add('listening');
            wave().classList.add('listening');
            stateLabel().textContent = 'LISTENING…';
            setTab('user');
        } else if (mode === 'thinking') {
            wave().className = '';
            stateLabel().textContent = 'THINKING…';
        } else {
            wave().className = '';
            stateLabel().textContent = 'READY';
        }
    }

    function updateProgress() {
        const pct = questions.length > 0 ? (idx / questions.length) * 100 : 0;
        progFill().style.width = pct + '%';
        scoreEl().textContent  = `${score} / ${answered} correct`;
    }

    function getCorrectAnswer(q) {
        const type = (q.questionType || 'mcq').toLowerCase();
        if (type === 'fillblank') return q.answerText || q.correctAnswer || '';
        if (type === 'truefalse') {
            return (q.correctAnswer || '').toUpperCase() === 'A' ? 'True' : 'False';
        }
        const map = { A: q.choiceA, B: q.choiceB, C: q.choiceC, D: q.choiceD };
        return map[(q.correctAnswer || '').toUpperCase()] || q.correctAnswer || '';
    }

    function buildQuestionText(q) {
        const type = (q.questionType || 'mcq').toLowerCase();
        let text = q.question;
        if (type === 'truefalse') {
            text += ' — True or False?';
        } else if (type === 'mcq') {
            const opts = [q.choiceA, q.choiceB, q.choiceC, q.choiceD].filter(Boolean);
            if (opts.length) text += ' Options: ' + opts.map((o, i) => `${['A','B','C','D'][i]}: ${o}`).join(', ');
        }
        return text;
    }

    /* ── Load saved voice from Profile settings ── */
    function loadSavedVoice() {
        const savedName = localStorage.getItem('sbVoiceName');
        // Only include voices that have a name and actually produce audio
        const voices = synth.getVoices().filter(v =>
            v.lang.startsWith('en') &&
            v.name &&
            v.name.trim() !== '' &&
            !v.name.toLowerCase().includes('espeak') // exclude known silent/robotic fallbacks
        );

        if (!voices.length) {
            selectedVoice = null;
            return;
        }

        if (savedName) {
            selectedVoice = voices.find(v => v.name === savedName);
        }

        // Fallback: pick first available voice if saved one not found
        if (!selectedVoice) {
            selectedVoice = voices[0];
        }
    }

    /* ── TTS ── */
    function speak(text, onEnd) {
        synth.cancel();
        const utt = new SpeechSynthesisUtterance(text);
        utt.rate  = 0.95;
        utt.pitch = 1.05;
        if (selectedVoice) utt.voice = selectedVoice;
        utt.onstart = () => setOrb('speaking');
        utt.onend   = () => { if (onEnd) onEnd(); };
        utt.onerror = () => { if (onEnd) onEnd(); };
        synth.speak(utt);
    }

    /* ── STT ── */
    function initRecognition() {
        const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SR) { speechSupported = false; return; }
        speechSupported = true;
        recognition = new SR();
        recognition.lang = 'en-US';
        recognition.continuous = false;
        recognition.interimResults = true;

        recognition.onstart  = () => setOrb('listening');
        recognition.onresult = e => {
            let interim = '', final = '';
            for (let r of e.results) {
                if (r.isFinal) final += r[0].transcript;
                else interim += r[0].transcript;
            }
            transcript().textContent = '"' + (final || interim) + '"';
            if (final) handleAnswer(final);
        };
        recognition.onerror = e => {
            if (e.error === 'no-speech') {
                transcript().textContent = 'No speech detected — try again or type below.';
                setOrb('idle');
                showFallback();
            }
        };
        recognition.onend = () => { if (waitingAns) setOrb('idle'); };
    }

    function startListening() {
        if (!speechSupported) { showFallback(); return; }
        try { recognition.start(); } catch(e) { /* already running */ }
    }

    function stopListening() {
        if (recognition) try { recognition.stop(); } catch(e) {}
    }

    function showFallback() {
        fallbackRow().style.display = 'flex';
        $('sbFallbackInput').focus();
    }

    /* ── Gemini feedback ── */
    async function getGeminiFeedback(question, correctAnswer, userAnswer, isCorrect) {
        const prompt = `You are a warm, encouraging study buddy helping a student review quiz material.

Question: "${question}"
Correct answer: "${correctAnswer}"
Student answered: "${userAnswer}"
Result: ${isCorrect ? 'CORRECT' : 'INCORRECT'}

Give a SHORT, natural, conversational response (2-3 sentences max):
- If correct: celebrate briefly, then add one interesting insight or tip about the topic.
- If wrong: be kind, give the correct answer clearly, and give a quick memory tip.
Be friendly and encouraging. No bullet points. Speak naturally as if talking to a friend.`;

        try {
            const res = await fetch(GEMINI_URL, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    contents: [{ parts: [{ text: prompt }] }],
                    generationConfig: { maxOutputTokens: 150, temperature: 0.8 }
                })
            });
            const data = await res.json();
            return data.candidates?.[0]?.content?.parts?.[0]?.text
                || (isCorrect ? 'Correct! Great job.' : `Not quite — the answer is: ${correctAnswer}.`);
        } catch {
            return isCorrect
                ? 'Correct! Well done.'
                : `Not quite — the correct answer is: ${correctAnswer}. Keep going!`;
        }
    }

    /* ── Quiz flow ── */
    async function showQuestion() {
        if (closed) return;
        if (idx >= questions.length) { showResults(); return; }

        const q   = questions[idx];
        const num = idx + 1;
        waitingAns = true;
        fallbackRow().style.display = 'none';
        feedback().style.display    = 'none';
        transcript().textContent    = '';
        $('sbFallbackInput').value  = '';

        qNum().textContent       = `QUESTION ${num} OF ${questions.length}`;
        qText().textContent      = q.question;
        $('sbSubtitle').textContent = `${quizLabel} · Q${num}/${questions.length}`;
        updateProgress();

        const spokenQ = buildQuestionText(q);
        const intro   = num === 1
            ? `Alright, let's get started! Question ${num}. ${spokenQ}`
            : `Question ${num}. ${spokenQ}`;

        speak(intro, () => {
            if (!waitingAns || closed) return;
            setOrb('idle');
            stateLabel().textContent = 'YOUR TURN';
            setTab('user');
            startListening();
        });
    }

    async function handleAnswer(userAnswer) {
        if (!waitingAns) return;
        waitingAns = false;
        stopListening();
        setOrb('thinking');

        const q         = questions[idx];
        const correct   = getCorrectAnswer(q);
        const ua        = userAnswer.trim().toLowerCase();
        const ca        = correct.trim().toLowerCase();
        const isCorrect = ua === ca
            || (ca.length > 3 && ua.length > 3 && (ca.includes(ua) || ua.includes(ca)));

        answered++;
        if (isCorrect) score++;
        updateProgress();

        const fbText = await getGeminiFeedback(q.question, correct, userAnswer, isCorrect);

        feedback().className = '';
        feedback().classList.add(isCorrect ? 'correct' : 'wrong');
        feedback().textContent    = fbText;
        feedback().style.display  = 'block';

        speak(fbText, () => {
            if (closed) return;
            setOrb('idle');
            stateLabel().textContent = 'READY';
            idx++;
            setTimeout(showQuestion, 800);
        });
    }

    function showResults() {
        stage().style.display   = 'none';
        footer().style.display  = 'none';
        results().style.display = 'flex';
        progFill().style.width  = '100%';

        const pct = questions.length > 0 ? Math.round((score / questions.length) * 100) : 0;
        $('sbResScore').textContent = `${score}/${questions.length}`;
        $('sbResTitle').textContent = pct >= 80 ? '🎉 Excellent work!' : pct >= 50 ? '👍 Good effort!' : '💪 Keep practicing!';
        $('sbResSub').textContent   = `You got ${score} out of ${questions.length} correct (${pct}%).`;

        const msg = pct >= 80
            ? `Amazing! You got ${score} out of ${questions.length}. That's ${pct} percent. You're really mastering this material!`
            : pct >= 50
            ? `Good work! You got ${score} out of ${questions.length}. Keep reviewing and you'll nail it next time.`
            : `You got ${score} out of ${questions.length}. Don't worry — reviewing is how we learn. Try again!`;
        speak(msg);
    }

    /* ── Opening greeting ── */
    function SB_startGreeting() {
        const greeting = `Hi! I'm your Study Buddy! I'll be quizzing you on ${quizLabel}. Say "ready" when you want to begin!`;
        speak(greeting, () => {
            stateLabel().textContent = 'SAY "READY" TO BEGIN';
            setTab('user');
            waitingAns = false;

            const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
            if (SR) {
                const r = new SR();
                r.lang     = 'en-US';
                r.onresult = () => { r.stop(); showQuestion(); };
                r.onerror  = ()  => showQuestion();
                r.onend    = ()  => {};
                try { r.start(); } catch(e) { showQuestion(); }
            } else {
                const btn = document.createElement('button');
                btn.textContent = '▶ Start Quiz';
                btn.className   = 'sb-btn';
                btn.style.cssText = 'background:var(--accent);border:none;color:#fff;margin-top:4px;';
                btn.onclick = () => { btn.remove(); showQuestion(); };
                stage().appendChild(btn);
            }
        });
    }

    /* ══════════════════════════════════════
       PUBLIC API
    ══════════════════════════════════════ */
    return {

        open(label, qs) {
            closed    = false;
            quizLabel = label;
            questions = qs;
            idx = 0; score = 0; answered = 0; waitingAns = false;

            // Reset panels
            stage().style.display   = 'none';
            results().style.display = 'none';
            footer().style.display  = 'none';
            feedback().style.display    = 'none';
            transcript().textContent    = '';
            fallbackRow().style.display = 'none';
            progFill().style.width      = '0%';
            scoreEl().textContent       = '0 / 0 correct';
            $('sbTitle').textContent    = 'Study Buddy';
            $('sbSubtitle').textContent = label;

            overlay().classList.add('open');
            initRecognition();

            // Load saved voice, then start immediately
            const startSession = () => {
                loadSavedVoice();
                stage().style.display  = 'flex';
                footer().style.display = 'flex';
                SB_startGreeting();
            };

            if (synth.getVoices().length > 0) {
                startSession();
            } else {
                synth.onvoiceschanged = () => { synth.onvoiceschanged = null; startSession(); };
                setTimeout(() => { if (stage().style.display === 'none') startSession(); }, 800);
            }
        },

        close() {
            closed = true;
            synth.cancel();
            stopListening();
            waitingAns = false;
            overlay().classList.remove('open');
        },

        skip() {
            synth.cancel();
            stopListening();
            waitingAns = false;
            idx++;
            setTimeout(showQuestion, 300);
        },

        toggleMic() {
            if (!speechSupported) { showFallback(); return; }
            startListening();
        },

        submitTextAnswer() {
            const val = $('sbFallbackInput').value.trim();
            if (!val) return;
            transcript().textContent    = `"${val}"`;
            fallbackRow().style.display = 'none';
            handleAnswer(val);
        },

        retry() {
            results().style.display = 'none';
            stage().style.display   = 'flex';
            footer().style.display  = 'flex';
            idx = 0; score = 0; answered = 0;
            updateProgress();
            setTimeout(showQuestion, 300);
        }
    };

})();