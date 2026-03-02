const tokenKey = "circlehub-token";
let token = localStorage.getItem(tokenKey) || "";
let selectedUserId = null;

const el = {
  authCard: document.querySelector("#authCard"),
  userCard: document.querySelector("#userCard"),
  usersCard: document.querySelector("#usersCard"),
  appArea: document.querySelector("#appArea"),
  messagesArea: document.querySelector("#messagesArea"),
  authStatus: document.querySelector("#authStatus"),
  logoutBtn: document.querySelector("#logoutBtn"),
  loginForm: document.querySelector("#loginForm"),
  registerForm: document.querySelector("#registerForm"),
  meName: document.querySelector("#meName"),
  meEmail: document.querySelector("#meEmail"),
  usersList: document.querySelector("#usersList"),
  pendingList: document.querySelector("#pendingList"),
  feedList: document.querySelector("#feedList"),
  postForm: document.querySelector("#postForm"),
  postContent: document.querySelector("#postContent"),
  searchUsers: document.querySelector("#searchUsers"),
  connectionsList: document.querySelector("#connectionsList"),
  messageList: document.querySelector("#messageList"),
  messageForm: document.querySelector("#messageForm"),
  messageInput: document.querySelector("#messageInput"),
  chatWith: document.querySelector("#chatWith"),
  postTemplate: document.querySelector("#postTemplate")
};

init();

async function init() {
  bindEvents();
  if (token) {
    const ok = await hydrateSession();
    if (!ok) clearSession();
  }
}

function bindEvents() {
  el.loginForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    try {
      const payload = {
        email: document.querySelector("#loginEmail").value,
        password: document.querySelector("#loginPassword").value
      };
      const data = await api("/api/auth/login", { method: "POST", body: JSON.stringify(payload) }, false);
      setSession(data.token);
      await hydrateSession();
      el.authStatus.textContent = "Signed in";
      el.loginForm.reset();
    } catch {
      el.authStatus.textContent = "Login failed.";
    }
  });

  el.registerForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    try {
      const payload = {
        name: document.querySelector("#registerName").value,
        email: document.querySelector("#registerEmail").value,
        password: document.querySelector("#registerPassword").value
      };
      const data = await api("/api/auth/register", { method: "POST", body: JSON.stringify(payload) }, false);
      setSession(data.token);
      await hydrateSession();
      el.authStatus.textContent = "Account created";
      el.registerForm.reset();
    } catch {
      el.authStatus.textContent = "Registration failed.";
    }
  });

  el.logoutBtn.addEventListener("click", () => {
    clearSession();
    el.authStatus.textContent = "Signed out.";
  });

  el.postForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    await api("/api/posts", { method: "POST", body: JSON.stringify({ content: el.postContent.value }) });
    el.postForm.reset();
    await loadFeed();
  });

  el.searchUsers.addEventListener("input", () => loadUsers(el.searchUsers.value));

  el.usersList.addEventListener("click", async (e) => {
    const userId = e.target.dataset.connect;
    if (!userId) return;
    await api(`/api/connections/request/${userId}`, { method: "POST" });
    await loadUsers(el.searchUsers.value);
  });

  el.pendingList.addEventListener("click", async (e) => {
    const connectionId = e.target.dataset.accept;
    if (!connectionId) return;
    await api(`/api/connections/accept/${connectionId}`, { method: "POST" });
    await loadConnections();
    await loadFeed();
  });

  el.connectionsList.addEventListener("click", async (e) => {
    const userId = e.target.dataset.chat;
    const name = e.target.dataset.name;
    if (!userId) return;
    selectedUserId = Number(userId);
    el.chatWith.textContent = `Chat with ${name}`;
    await loadMessages();
  });

  el.messageForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    if (!selectedUserId) return;
    await api(`/api/messages/${selectedUserId}`, { method: "POST", body: JSON.stringify({ content: el.messageInput.value }) });
    el.messageInput.value = "";
    await loadMessages();
  });
}

async function hydrateSession() {
  try {
    const me = await api("/api/me");
    el.meName.textContent = me.name;
    el.meEmail.textContent = me.email;
    setAuthUi(true);
    await Promise.all([loadUsers(), loadConnections(), loadFeed()]);
    return true;
  } catch {
    return false;
  }
}

function setAuthUi(signedIn) {
  el.authCard.classList.toggle("hidden", signedIn);
  el.userCard.classList.toggle("hidden", !signedIn);
  el.usersCard.classList.toggle("hidden", !signedIn);
  el.appArea.classList.toggle("hidden", !signedIn);
  el.messagesArea.classList.toggle("hidden", !signedIn);
  el.logoutBtn.classList.toggle("hidden", !signedIn);
}

async function loadUsers(query = "") {
  const users = await api(`/api/users?q=${encodeURIComponent(query)}`);
  el.usersList.innerHTML = users.map((u) => `<li><strong>${u.name}</strong><br>${u.email}<br><button class="secondary" data-connect="${u.id}">Connect</button></li>`).join("");
}

async function loadConnections() {
  const data = await api("/api/connections");
  el.pendingList.innerHTML = data.pendingReceived.length
    ? data.pendingReceived.map((r) => `<li>${r.fromName} <button data-accept="${r.id}">Accept</button></li>`).join("")
    : "<li>No pending requests</li>";

  el.connectionsList.innerHTML = data.accepted.length
    ? data.accepted.map((c) => `<li><button class="secondary" data-chat="${c.userId}" data-name="${c.name}">${c.name}</button></li>`).join("")
    : "<li>No accepted connections yet</li>";
}

async function loadFeed() {
  const posts = await api("/api/feed");
  el.feedList.innerHTML = "";
  posts.forEach((p) => {
    const node = el.postTemplate.content.firstElementChild.cloneNode(true);
    node.querySelector(".post-author").textContent = p.author;
    node.querySelector(".post-time").textContent = new Date(p.createdAt).toLocaleString();
    node.querySelector(".post-content").textContent = p.content;
    el.feedList.append(node);
  });
}

async function loadMessages() {
  if (!selectedUserId) return;
  const messages = await api(`/api/messages/${selectedUserId}`);
  el.messageList.innerHTML = messages.map((m) => `<li>${new Date(m.sentAt).toLocaleTimeString()} • ${m.content}</li>`).join("");
}

function setSession(newToken) {
  token = newToken;
  localStorage.setItem(tokenKey, token);
}

function clearSession() {
  token = "";
  localStorage.removeItem(tokenKey);
  setAuthUi(false);
}

async function api(url, options = {}, needsAuth = true) {
  const headers = { "Content-Type": "application/json", ...(options.headers || {}) };
  if (needsAuth && token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(url, { ...options, headers });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.status === 204 ? null : res.json();
}
