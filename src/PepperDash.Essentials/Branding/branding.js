// Brands the Essentials dev tools app that is extracted to html/debug. The app's sign-in heading is
// baked into its own bundle, so the text is swapped in the rendered DOM and re-applied whenever the
// app re-renders. The logo comes from the program's logo folder - see DevToolsBranding.
(function () {
  var ORIGINAL_TITLE = "PepperDash Essentials Developer Tools";
  var BRAND_TITLE = "Beincourt Essentials Development Tools";
  var LOGO_WIDTH_RATIO = 0.8; // logo width, as a share of the rendered title's width
  var LOGO_ID = "brand-logo";
  var logoUrl = document.currentScript ? document.currentScript.getAttribute("data-logo") : null;
  var observer = null;
  var scheduled = false;

  function titleElement() {
    var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, null);
    var node;
    while ((node = walker.nextNode())) {
      var text = node.nodeValue ? node.nodeValue.trim() : "";
      if (text === ORIGINAL_TITLE || text === BRAND_TITLE) {
        if (text === ORIGINAL_TITLE) node.nodeValue = node.nodeValue.replace(ORIGINAL_TITLE, BRAND_TITLE);
        return node.parentElement;
      }
    }
    return null;
  }

  // The heading is a full-width block, so measure the text itself rather than the element.
  function textWidth(element) {
    var style = window.getComputedStyle(element);
    var probe = document.createElement("span");
    probe.textContent = BRAND_TITLE;
    probe.style.position = "absolute";
    probe.style.visibility = "hidden";
    probe.style.whiteSpace = "pre";
    probe.style.fontFamily = style.fontFamily;
    probe.style.fontSize = style.fontSize;
    probe.style.fontWeight = style.fontWeight;
    probe.style.letterSpacing = style.letterSpacing;
    document.body.appendChild(probe);
    var width = probe.getBoundingClientRect().width;
    document.body.removeChild(probe);
    return width;
  }

  function brand() {
    var element = titleElement();
    if (!element || !logoUrl) return;

    var logo = document.getElementById(LOGO_ID);
    if (!logo) {
      logo = document.createElement("img");
      logo.id = LOGO_ID;
      logo.src = logoUrl;
      logo.alt = "";
      logo.style.display = "block";
      logo.style.margin = "0 auto 12px";
      logo.style.height = "auto";
      element.parentNode.insertBefore(logo, element);
    }

    var width = Math.round(textWidth(element) * LOGO_WIDTH_RATIO);
    // Only write the width when it actually changes: every write is itself a mutation, and writing
    // unconditionally from the observer would loop.
    if (width > 0 && logo.style.width !== width + "px") logo.style.width = width + "px";
  }

  // The observer must not see this script's own edits, or branding would re-trigger itself forever.
  function apply() {
    if (observer) observer.disconnect();
    try {
      brand();
    } finally {
      if (observer) observer.observe(document.body, { childList: true, subtree: true, characterData: true });
    }
  }

  function schedule() {
    if (scheduled) return;
    scheduled = true;
    window.requestAnimationFrame(function () {
      scheduled = false;
      apply();
    });
  }

  function start() {
    observer = new MutationObserver(schedule);
    apply();
    window.addEventListener("resize", schedule);
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
  else start();
})();
