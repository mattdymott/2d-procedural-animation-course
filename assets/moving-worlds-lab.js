/* A reusable workshop lab for testing support-relative foot promises. */
var MovingWorldsLab = (function () {
  'use strict';

  function mount(root) {
    var canvas = root.querySelector('canvas');
    var ctx = canvas.getContext('2d');
    var support = root.querySelector('[data-support]');
    var storage = root.querySelector('[data-storage]');
    var time = root.querySelector('[data-time]');
    var timeValue = root.querySelector('[data-time-value]');
    var release = root.querySelector('[data-release]');
    var readout = root.querySelector('[data-readout]');
    var state = { released: false };

    function pose() {
      var t = Number(time.value) / 100;
      if (support.value === 'elevator') return { x: 350, y: 290 - Math.sin(t * Math.PI * 2) * 105, a: 0, surface: 0, vx: 0, vy: -Math.cos(t * Math.PI * 2) * 6.6 };
      if (support.value === 'conveyor') return { x: 350, y: 275, a: 0, surface: t * 155, vx: 0, vy: 0 };
      return { x: 350, y: 265, a: t * Math.PI * 2, surface: 0, vx: 0, vy: 0 };
    }

    function rotate(point, angle) {
      return { x: point.x * Math.cos(angle) - point.y * Math.sin(angle), y: point.x * Math.sin(angle) + point.y * Math.cos(angle) };
    }

    function drawArrow(from, vector, colour, label) {
      var scale = 8, to = { x: from.x + vector.x * scale, y: from.y + vector.y * scale };
      ctx.strokeStyle = colour; ctx.fillStyle = colour; ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(from.x, from.y); ctx.lineTo(to.x, to.y); ctx.stroke();
      var angle = Math.atan2(vector.y, vector.x);
      ctx.beginPath(); ctx.moveTo(to.x, to.y); ctx.lineTo(to.x - 8 * Math.cos(angle - .45), to.y - 8 * Math.sin(angle - .45)); ctx.lineTo(to.x - 8 * Math.cos(angle + .45), to.y - 8 * Math.sin(angle + .45)); ctx.closePath(); ctx.fill();
      ctx.font = '12px system-ui'; ctx.fillText(label, to.x + 8, to.y - 4);
    }

    function draw() {
      var p = pose(), local = { x: -54, y: -20 + p.surface };
      var rotated = rotate(local, p.a);
      var correct = { x: p.x + rotated.x, y: p.y + rotated.y };
      var frozen = { x: 296, y: 255 };
      var requery = { x: correct.x + Math.sin(Number(time.value) * .55) * 22, y: correct.y + Math.cos(Number(time.value) * .42) * 12 };
      var foot = storage.value === 'relative' ? correct : (storage.value === 'requery' ? requery : frozen);
      var carry = { x: p.vx, y: p.vy };
      if (support.value === 'conveyor') carry.x += 4.2;
      if (support.value === 'turntable') {
        carry.x = -rotated.y * .05;
        carry.y = rotated.x * .05;
      }
      timeValue.value = time.value + '%';
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      ctx.fillStyle = '#17232f'; ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.strokeStyle = '#274052'; ctx.lineWidth = 1;
      for (var x = 0; x < canvas.width; x += 35) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, canvas.height); ctx.stroke(); }
      for (var y = 0; y < canvas.height; y += 35) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(canvas.width, y); ctx.stroke(); }

      ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(p.a);
      ctx.fillStyle = '#657b8d';
      if (support.value === 'turntable') { ctx.beginPath(); ctx.arc(0, 0, 105, 0, Math.PI * 2); ctx.fill(); }
      else { ctx.fillRect(-145, -12, 290, 24); }
      if (support.value === 'conveyor') {
        ctx.strokeStyle = '#f2c265'; ctx.lineWidth = 3;
        for (var b = -120; b < 135; b += 42) { ctx.beginPath(); ctx.moveTo(b, 0); ctx.lineTo(b + 16, 0); ctx.stroke(); }
      }
      ctx.restore();

      var body = { x: 455, y: 138 };
      ctx.strokeStyle = '#70c7c0'; ctx.lineWidth = 7; ctx.lineCap = 'round'; ctx.beginPath(); ctx.moveTo(body.x - 28, body.y + 6); ctx.lineTo(body.x + 46, body.y - 4); ctx.stroke();
      ctx.fillStyle = '#70c7c0'; ctx.beginPath(); ctx.arc(body.x, body.y, 23, 0, Math.PI * 2); ctx.fill();
      ctx.strokeStyle = '#d7e5ec'; ctx.lineWidth = 4; ctx.beginPath(); ctx.moveTo(body.x - 5, body.y + 22); ctx.lineTo(foot.x, foot.y); ctx.stroke();
      ctx.fillStyle = storage.value === 'relative' ? '#f1d272' : '#ec8174'; ctx.beginPath(); ctx.arc(foot.x, foot.y, 8, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = '#c4d7e2'; ctx.font = '13px system-ui'; ctx.fillText('committed foot target', foot.x + 12, foot.y - 11);

      if (storage.value !== 'relative') {
        ctx.strokeStyle = '#e57c6f'; ctx.setLineDash([6, 5]); ctx.beginPath(); ctx.moveTo(frozen.x, frozen.y); ctx.lineTo(correct.x, correct.y); ctx.stroke(); ctx.setLineDash([]);
      }
      if (state.released) drawArrow(foot, carry, '#f2c265', 'liftoff carry');

      var right = storage.value === 'relative';
      readout.className = 'workshop-readout ' + (right ? 'ok' : 'bad');
      readout.textContent = right
        ? 'Correct: the foot target is derived from one committed support-local promise. Change time or support type; gait has not made a new decision.'
        : (storage.value === 'requery'
          ? 'Bug visible: a fresh query is rewriting the target. It happens to follow the support, but it is no longer the contact promise made at landing.'
          : 'Bug visible: this foot is no longer keeping its promise relative to the support. The dashed line shows where a support-relative target would be.');
      root.dispatchEvent(new CustomEvent('movingworldslab:state', { detail: { correct: right, support: support.value, released: state.released } }));
    }

    [support, storage, time].forEach(function (control) { control.addEventListener('input', function () { state.released = false; draw(); }); control.addEventListener('change', function () { state.released = false; draw(); }); });
    release.addEventListener('click', function () { state.released = true; draw(); });
    draw();
  }
  return { mount: mount };
}());
