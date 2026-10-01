'use strict';

const form = document.getElementById('loginForm');
const errorBox = document.getElementById('loginError');
const submitBtn = document.getElementById('loginSubmit');

function showError(msg) {
  errorBox.textContent = msg;
  errorBox.classList.remove('hidden');
}

// Si ya hay una sesión válida (cookie viva), no tiene sentido mostrar el login.
(async () => {
  try {
    const r = await fetch('/api/v1/admin/auth/me', { credentials: 'include' });
    if (r.ok) window.location.replace('index.html');
  } catch { /* sin conexión: se queda en el login */ }
})();

form.addEventListener('submit', async e => {
  e.preventDefault();
  errorBox.classList.add('hidden');
  submitBtn.disabled = true;
  submitBtn.textContent = 'Ingresando…';

  const username = document.getElementById('loginUser').value.trim();
  const password = document.getElementById('loginPass').value;

  try {
    const r = await fetch('/api/v1/admin/auth/login', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    });
    if (!r.ok) {
      let msg = 'Usuario o contraseña inválidos.';
      try { const j = await r.json(); msg = j.error || msg; } catch {}
      showError(msg);
      return;
    }
    window.location.replace('index.html');
  } catch {
    showError('No se pudo contactar al servidor. Intente de nuevo.');
  } finally {
    submitBtn.disabled = false;
    submitBtn.textContent = 'Ingresar';
  }
});
