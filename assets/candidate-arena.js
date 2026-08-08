(function (global) {
  'use strict';

  function mount(root) {
    var canvas = root.querySelector('canvas');
    var ctx = canvas.getContext('2d');
    var heading = root.querySelector('[data-heading]');
    var headingValue = root.querySelector('[data-heading-value]');
    var queryButton = root.querySelector('[data-query]');
    var landButton = root.querySelector('[data-land]');
    var requery = root.querySelector('[data-requery]');
    var options = root.querySelector('[data-options]');
    var readout = root.querySelector('[data-readout]');
    var phase = 'Planted';
    var plant = { x: 235, y: 245 };
    var reserved = null;
    var candidates = [];
    var initialSelection = false;

    function radians() {
      return Number(heading.value) * Math.PI / 180;
    }
    function rotate(point, angle) {
      return {
        x: point.x * Math.cos(angle) - point.y * Math.sin(angle),
        y: point.x * Math.sin(angle) + point.y * Math.cos(angle)
      };
    }
    function home() {
      var r = rotate({ x: 82, y: 42 }, radians());
      return { x: 310 + r.x, y: 195 + r.y };
    }
    function makeCandidates() {
      var h = home();
      var forward = { x: Math.cos(radians()), y: Math.sin(radians()) };
      var side = { x: -forward.y, y: forward.x };
      return [
        { id: 'forward', label: 'Forward shelf', note: 'legal and closest to the desired home', legal: true, x: h.x + forward.x * 9, y: h.y + forward.y * 9 },
        { id: 'side', label: 'Side shelf', note: 'legal but farther from the desired home', legal: true, x: h.x + side.x * 55, y: h.y + side.y * 55 },
        { id: 'blocked', label: 'Blocked tile', note: 'inside the obstacle; never reserve it', legal: false, x: h.x - side.x * 36, y: h.y - side.y * 36 }
      ];
    }
    function distance(a, b) {
      return Math.hypot(a.x - b.x, a.y - b.y);
    }
    function currentReserved() {
      if (!reserved || !requery.checked) return reserved;
      var fresh = makeCandidates().filter(function (c) { return c.legal; });
      fresh.sort(function (a, b) { return distance(a, home()) - distance(b, home()); });
      return fresh[0];
    }
    function emit() {
      root.dispatchEvent(new CustomEvent('candidatearena:state', {
        detail: { phase: phase, reserved: reserved, requery: requery.checked, selected: initialSelection }
      }));
    }
    function setReadout(text, ok) {
      readout.className = ok ? 'decision-feedback ok' : 'decision-feedback';
      readout.textContent = text;
    }
    function renderOptions() {
      options.innerHTML = '';
      candidates.forEach(function (candidate) {
        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'candidate-option';
        button.disabled = !candidate.legal || phase !== 'Selecting';
        button.innerHTML = '<strong>' + candidate.label + '</strong><br><small>' + candidate.note + '</small>';
        if (reserved && candidate.id === reserved.id) button.classList.add('reserved');
        button.addEventListener('click', function () {
          reserved = { id:candidate.id, label:candidate.label, x:candidate.x, y:candidate.y };
          phase = 'Swing';
          initialSelection = true;
          setReadout('Reserved ' + candidate.label + '. Change heading now: a correct swing keeps this world point.', true);
          renderOptions();
          draw();
          emit();
        });
        options.appendChild(button);
      });
    }
    function line(a, b, color, dash) {
      ctx.save();
      ctx.strokeStyle = color;
      ctx.lineWidth = 2;
      ctx.setLineDash(dash ? [7, 5] : []);
      ctx.beginPath();
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
      ctx.stroke();
      ctx.restore();
    }
    function dot(point, radius, stroke, fill) {
      ctx.save();
      ctx.lineWidth = 2;
      ctx.strokeStyle = stroke;
      ctx.fillStyle = fill;
      ctx.beginPath();
      ctx.arc(point.x, point.y, radius, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
      ctx.restore();
    }
    function label(text, point, color) {
      ctx.fillStyle = color;
      ctx.font = '12px sans-serif';
      ctx.fillText(text, point.x + 9, point.y - 9);
    }
    function draw() {
      var h = home();
      var shownReserved = currentReserved();
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      ctx.fillStyle = '#101820';
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.strokeStyle = '#1e3740';
      for (var x = 10; x < canvas.width; x += 25) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, canvas.height); ctx.stroke(); }
      for (var y = 10; y < canvas.height; y += 25) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(canvas.width, y); ctx.stroke(); }
      var obstacle = candidates.filter(function (c) { return c.id === 'blocked'; })[0];
      if (obstacle) {
        ctx.fillStyle = '#5e3238';
        ctx.fillRect(obstacle.x - 22, obstacle.y - 22, 44, 44);
        ctx.strokeStyle = '#ff817b';
        ctx.strokeRect(obstacle.x - 22, obstacle.y - 22, 44, 44);
      }
      candidates.forEach(function (candidate) {
        if (candidate.id !== 'blocked') dot(candidate, 7, '#77d7ae', '#255a49');
        if (candidate.id === 'blocked') dot(candidate, 7, '#ff817b', '#5e3238');
      });
      line(plant, h, '#e7c866', true);
      dot(h, 8, '#e7c866', '#463d1d');
      label('desired home', h, '#e7c866');
      dot(plant, 7, '#d9f0ff', '#486878');
      label('committed plant', plant, '#d9f0ff');
      if (shownReserved) {
        line(plant, shownReserved, '#ba83ff', false);
        dot(shownReserved, 9, '#d9a8ff', '#5c347a');
        label(requery.checked ? 'BUG: refreshed candidate' : 'reserved candidate', shownReserved, '#d9a8ff');
      }
      ctx.save();
      ctx.translate(310, 195);
      ctx.rotate(radians());
      ctx.fillStyle = '#5ca7d8';
      ctx.strokeStyle = '#d7efff';
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.ellipse(0, 0, 42, 27, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
      ctx.fillStyle = '#f5cf65';
      ctx.beginPath();
      ctx.moveTo(48, 0);
      ctx.lineTo(24, -10);
      ctx.lineTo(24, 10);
      ctx.closePath();
      ctx.fill();
      ctx.restore();
      headingValue.value = heading.value + ' degrees';
      landButton.disabled = phase !== 'Swing';
    }
    heading.addEventListener('input', draw);
    requery.addEventListener('change', function () {
      if (phase === 'Swing' && requery.checked) {
        setReadout('Bug enabled: the stored candidate is being overwritten by a fresh query during swing.', false);
      } else if (phase === 'Swing') {
        setReadout('Correct again: the purple reservation remains fixed while the home keeps moving.', true);
      }
      draw();
      emit();
    });
    queryButton.addEventListener('click', function () {
      candidates = makeCandidates();
      reserved = null;
      phase = 'Selecting';
      setReadout('Query captured three results. Reserve one legal candidate; the blocked tile is not selectable.', false);
      renderOptions();
      draw();
      emit();
    });
    landButton.addEventListener('click', function () {
      var landed = currentReserved();
      if (!landed) return;
      plant = { x:landed.x, y:landed.y };
      phase = 'Planted';
      reserved = null;
      requery.checked = false;
      setReadout('Landing committed. The plant changed once, at swing completion, to the reserved world point.', true);
      renderOptions();
      draw();
      emit();
    });
    candidates = makeCandidates();
    renderOptions();
    draw();
  }

  global.CandidateArena = { mount: mount };
}(window));

