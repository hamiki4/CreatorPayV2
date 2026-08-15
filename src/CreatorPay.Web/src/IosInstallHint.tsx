import { useEffect, useState } from "react";

const dismissedKey = "weymela_ios_install_hint_dismissed";

function isIosSafariBrowser() {
  const navigatorWithStandalone = navigator as Navigator & { standalone?: boolean };
  const iosDevice = /iPhone|iPad|iPod/i.test(navigator.userAgent)
    || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
  const safari = /Safari/i.test(navigator.userAgent)
    && !/CriOS|FxiOS|EdgiOS|OPiOS/i.test(navigator.userAgent);
  const standalone = navigatorWithStandalone.standalone === true
    || window.matchMedia("(display-mode: standalone)").matches;
  return iosDevice && safari && !standalone;
}

export function IosInstallHint() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    setVisible(isIosSafariBrowser() && localStorage.getItem(dismissedKey) !== "1");
  }, []);

  if (!visible) return null;
  return (
    <aside className="ios-install-hint" aria-label="Install Weymela on iPhone">
      <span><strong>Install Weymela on your iPhone</strong><br />Safari → Share → Add to Home Screen</span>
      <button
        type="button"
        aria-label="Dismiss iPhone installation instructions"
        onClick={() => {
          localStorage.setItem(dismissedKey, "1");
          setVisible(false);
        }}
      >Dismiss</button>
    </aside>
  );
}
