/* Reusable retrieval-practice quiz widget for course lessons.
 *
 * Usage in a lesson:
 *   <div id="quiz1"></div>
 *   <script src="../assets/quiz.js"></script>
 *   <script>
 *     Quiz.render(document.getElementById('quiz1'), [
 *       {
 *         prompt: "Question text?",
 *         options: ["A", "B", "C"],   // keep answers the same length!
 *         answer: 1,                   // index into options
 *         explain: "Shown after any answer is chosen."
 *       },
 *     ]);
 *   </script>
 *
 * Options are shuffled on each render so position gives no clue.
 * Feedback is immediate (green/red) and the explanation always appears,
 * so a wrong answer still ends in learning.
 */
(function () {
  'use strict';

  function shuffle(indices) {
    for (let i = indices.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [indices[i], indices[j]] = [indices[j], indices[i]];
    }
    return indices;
  }

  function renderQuestion(q, num) {
    const wrap = document.createElement('div');
    wrap.className = 'quiz-q';

    const prompt = document.createElement('p');
    prompt.className = 'prompt';
    prompt.textContent = num + '. ' + q.prompt;
    wrap.appendChild(prompt);

    const order = shuffle(q.options.map((_, i) => i));
    const buttons = [];

    order.forEach((optIndex) => {
      const btn = document.createElement('button');
      btn.className = 'option';
      btn.type = 'button';
      btn.textContent = q.options[optIndex];
      btn.addEventListener('click', () => {
        buttons.forEach((b) => { b.disabled = true; });
        if (optIndex === q.answer) {
          btn.classList.add('correct');
        } else {
          btn.classList.add('wrong');
          const correctBtn = buttons.find((b) => b.dataset.opt == q.answer);
          if (correctBtn) correctBtn.classList.add('correct');
        }
        if (q.explain) {
          const ex = document.createElement('p');
          ex.className = 'explain';
          ex.textContent = q.explain;
          wrap.appendChild(ex);
        }
      });
      btn.dataset.opt = optIndex;
      buttons.push(btn);
      wrap.appendChild(btn);
    });

    return wrap;
  }

  window.Quiz = {
    render(container, questions) {
      container.classList.add('quiz');
      questions.forEach((q, i) => container.appendChild(renderQuestion(q, i + 1)));
    },
  };
})();
