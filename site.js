(() => {
  'use strict';
  const byId = id => document.getElementById(id);
  const menu = byId('menu-panel'), install = byId('install-button'), help = byId('install-help');
  const retry = byId('retry-button'), status = byId('status-message'), frame = byId('game');
  let installPrompt = null, checking = false, gameStarted = false;
  const standalone = window.matchMedia('(display-mode: standalone)');
  function updateInstalled() {
    const installed = standalone.matches || navigator.standalone === true;
    install.disabled = installed;
    install.textContent = installed ? '홈 화면 앱으로 실행 중' : '홈 화면에 추가';
  }
  function closeMenu() { menu.close(); byId('menu-button').focus(); }
  byId('menu-button').addEventListener('click', () => menu.showModal());
  byId('close-menu').addEventListener('click', closeMenu);
  menu.addEventListener('click', event => {
    if (event.target !== menu) return;
    const r = menu.getBoundingClientRect();
    if (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom) closeMenu();
  });
  window.addEventListener('beforeinstallprompt', event => {
    event.preventDefault(); installPrompt = event; updateInstalled();
  });
  window.addEventListener('appinstalled', () => {
    installPrompt = null; help.hidden = false; help.textContent = '홈 화면에 FarmFarm이 추가되었습니다.';
  });
  standalone.addEventListener('change', updateInstalled);
  install.addEventListener('click', async () => {
    help.hidden = false;
    if (installPrompt) {
      const prompt = installPrompt; installPrompt = null; install.disabled = true;
      try {
        await prompt.prompt();
        const choice = await prompt.userChoice;
        help.textContent = choice.outcome === 'accepted' ? '설치 요청을 보냈습니다. 홈 화면을 확인해주세요.' : '나중에 브라우저 메뉴에서 추가할 수 있어요.';
      } catch { help.textContent = '브라우저 메뉴에서 앱 설치 또는 홈 화면에 추가를 선택해주세요.'; }
      finally { updateInstalled(); }
      return;
    }
    const apple = /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    help.textContent = apple
      ? 'Safari에서 이 페이지를 열고 공유 버튼 → 홈 화면에 추가 → 추가를 선택해주세요.'
      : '브라우저 메뉴의 앱 설치 또는 홈 화면에 추가를 선택해주세요. 항목이 없다면 Chrome이나 Edge로 열어주세요.';
  });
  function resize() {
    // The keyboard may reduce the visual viewport. Do not scroll the body to compensate.
    document.documentElement.style.setProperty('--view-height', `${Math.round(window.visualViewport?.height || window.innerHeight)}px`);
  }
  window.addEventListener('resize', resize, { passive: true });
  window.visualViewport?.addEventListener('resize', resize, { passive: true });
  resize(); updateInstalled();
  async function startGame() {
    if (checking || gameStarted) return;
    checking = true; retry.hidden = true; status.textContent = '게임을 확인하고 있어요.';
    const abort = new AbortController(), timeout = setTimeout(() => abort.abort(), 12000);
    try {
      // Build output lives in game/. Never overwrite this wrapper when rebuilding Unity.
      const response = await fetch('./game/index.html', { method: 'HEAD', cache: 'no-store', signal: abort.signal });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      frame.src = './game/index.html'; frame.hidden = false; gameStarted = true;
      byId('game-status').hidden = true;
    } catch {
      status.textContent = navigator.onLine === false ? '인터넷 연결을 확인해주세요.' : '게임을 아직 열 수 없어요.\n게임 파일 준비 중이거나 연결이 원활하지 않습니다.';
      retry.hidden = false;
    } finally { clearTimeout(timeout); checking = false; }
  }
  retry.addEventListener('click', startGame);
  // Cache only the small page shell, never Google/PlayFab responses or Unity save/build files.
  if ('serviceWorker' in navigator && window.isSecureContext) navigator.serviceWorker.register('./sw.js', { scope: './' }).catch(() => {});
  startGame();
})();
