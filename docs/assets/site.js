// Copy buttons for the donation addresses. The page works without this script.
(function () {
  var buttons = document.querySelectorAll('.copy');
  Array.prototype.forEach.call(buttons, function (b) {
    b.addEventListener('click', function () {
      var text = b.getAttribute('data-copy');
      var label = b.textContent;
      var done = function () {
        b.textContent = b.getAttribute('data-done');
        setTimeout(function () { b.textContent = label; }, 1800);
      };
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(done, function () {});
      } else {
        var t = document.createElement('textarea');
        t.value = text; document.body.appendChild(t); t.select();
        try { document.execCommand('copy'); done(); } catch (e) {}
        document.body.removeChild(t);
      }
    });
  });
})();
