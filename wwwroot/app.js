const tokenKey = "circlehub-token";
let token = localStorage.getItem(tokenKey) || "";
let activeChatUserId = null;
let refreshTimer = null;

const el = {
  authCard: document.querySelector("#authCard"),
  profileCard: document.querySelector("#profileCard"),
  pendingCard: document.querySelector("#pendingCard"),
  feedArea: document.querySelector("#feedArea"),
  socialArea: document.querySelector("#socialArea"),
  loginForm: document.querySelector("#loginForm"),
  registerForm: document.querySelector("#registerForm"),
  logoutBtn: document.querySelector("#logoutBtn"),
  authStatus: document.querySelector("#authStatus"),
  meName: document.querySelector("#meName"),
  meHeadline: document.querySelector("#meHeadline"),
  meEmail: document.querySelector("#meEmail"),
  searchUsers: document.querySelector("#searchUsers"),
  searchFeed: document.querySelector("#searchFeed"),
  pendingList: document.querySelector("#pendingList"),
  usersList: document.querySelector("#usersList"),
  connectionsList: document.querySelector("#connectionsList"),
  postForm: document.querySelector("#postForm"),
  postContent: document.querySelector("#postContent"),
  feedList: document.querySelector("#feedList"),
  feedEmpty: document.querySelector("#feedEmpty"),
  messageList: document.querySelector("#messageList"),
  messageForm: document.querySelector("#messageForm"),
  messageInput: document.querySelector("#messageInput"),
  chatWith: document.querySelector("#chatWith"),
  postTemplate: document.querySelector("#postTemplate")
};

bindEvents();
initialize();

async function initialize() {
  if (!token) {
    setSignedInState(false);
    return;
  }

  const hydrated = await loadDashboard();
  if (!hydrated) {
    clearSession();
  }
}

function bindEvents() {
  el.loginForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    const payload = {
      email: document.querySelector("#loginEmail").value,
      password: document.querySelector("#loginPassword").value
    };

    await authenticate("/api/auth/login", payload, "Signed in successfully.");
    el.loginForm.reset();
  });

  el.registerForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    const payload = {
      name: document.querySelector("#registerName").value,
      email: document.querySelector("#registerEmail").value,
      headline: document.querySelector("#registerHeadline").value,
      password: document.querySelector("#registerPassword").value
    };

    await authenticate("/api/auth/register", payload, "Account created.");
    el.registerForm.reset();
  });

  el.logoutBtn.addEventListener("click", async () => {
    try {
      await api("/api/auth/logout", { method: "POST" });
    } catch {
      // no-op: best-effort logout against backend
    }

    clearSession();
    setNotice("Signed out.");
  });

  el.searchUsers.addEventListener("input", async () => {
    if (!token) return;
    await loadPeople(el.searchUsers.value);
  });

  el.searchFeed.addEventListener("input", async () => {
    if (!token) return;
    await loadFeed(el.searchFeed.value);
  });

  el.postForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    await api("/api/posts", {
      method: "POST",
      body: JSON.stringify({ content: el.postContent.value })
    });
    el.postForm.reset();
    await loadFeed(el.searchFeed.value);
  });

  el.usersList.addEventListener("click", async (event) => {
    const { action, id } = event.target.dataset;
    if (!action || !id) return;

    if (action === "request") {
      await api(`/api/connections/request/${id}`, { method: "POST" });
      await loadPeople(el.searchUsers.value);
      return;
    }

    if (action === "accept") {
      await api(`/api/connections/${id}/accept`, { method: "POST" });
      await loadDashboard();
      return;
    }
  });

  el.pendingList.addEventListener("click", async (event) => {
    const { action, id } = event.target.dataset;
    if (!action || !id) return;

    if (action === "accept") {
      await api(`/api/connections/${id}/accept`, { method: "POST" });
    }

    if (action === "decline") {
      await api(`/api/connections/${id}/decline`, { method: "POST" });
    }

    await loadDashboard();
  });

  el.connectionsList.addEventListener("click", async (event) => {
    const userId = event.target.dataset.chat;
    const userName = event.target.dataset.name;
    if (!userId) return;

    activeChatUserId = Number(userId);
    el.chatWith.textContent = `Messages with ${userName}`;
    await loadMessages();
  });

  el.messageForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!activeChatUserId) return;

    await api(`/api/messages/${activeChatUserId}`, {
      method: "POST",
      body: JSON.stringify({ content: el.messageInput.value })
    });

    el.messageInput.value = "";
    await loadMessages();
  });
}

async function authenticate(url, payload, successMessage) {
  try {
    const result = await api(url, { method: "POST", body: JSON.stringify(payload) }, false);
    token = result.token;
    localStorage.setItem(tokenKey, token);
    setNotice(successMessage);
    await loadDashboard();
  } catch {
    setNotice("Authentication request failed.");
  }
}

async function loadDashboard() {
  try {
    const [me, bootstrap] = await Promise.all([api("/api/me"), api("/api/bootstrap")]);
    setSignedInState(true);
    el.meName.textContent = me.name;
    el.meHeadline.textContent = me.headline;
    el.meEmail.textContent = me.email;
    renderPendingRequests(bootstrap.pendingRequests);
    renderPeople(bootstrap.discoverPeople);
    renderConnections(bootstrap.connections);
    await loadFeed(el.searchFeed.value);
    setupMessageRefresh();
    return true;
  } catch {
    return false;
  }
}

async function loadPeople(query = "") {
  const people = await api(`/api/users?q=${encodeURIComponent(query)}`);
  renderPeople(people);
}

async function loadFeed(query = "") {
  const posts = await api(`/api/feed?q=${encodeURIComponent(query)}`);
  el.feedList.innerHTML = "";
  el.feedEmpty.classList.toggle("hidden", posts.length > 0);

  posts.forEach((post) => {
    const node = el.postTemplate.content.firstElementChild.cloneNode(true);
    node.querySelector(".post-author").textContent = post.author.name;
    node.querySelector(".post-headline").textContent = post.author.headline;
    node.querySelector(".post-time").textContent = new Date(post.createdAt).toLocaleString();
    node.querySelector(".post-content").textContent = post.content;
    el.feedList.append(node);
  });
}

async function loadMessages() {
  if (!activeChatUserId) {
    el.messageList.innerHTML = "<li>Select a connection to open a conversation.</li>";
    return;
  }

  const payload = await api(`/api/messages/${activeChatUserId}`);
  el.chatWith.textContent = `Messages with ${payload.conversationWith.name}`;
  el.messageList.innerHTML = payload.messages.length
    ? payload.messages.map((message) => `
        <li class="message-item ${message.direction}">
          <span>${message.content}</span>
          <small>${new Date(message.sentAt).toLocaleString()}</small>
        </li>
      `).join("")
    : "<li>No messages yet.</li>";
}

function renderPendingRequests(requests) {
  el.pendingList.innerHTML = requests.length
    ? requests.map((request) => `
        <li>
          <strong>${request.requester.name}</strong><br>
          <span>${request.requester.headline}</span><br>
          <div class="button-row">
            <button data-action="accept" data-id="${request.id}">Accept</button>
            <button class="secondary" data-action="decline" data-id="${request.id}">Decline</button>
          </div>
        </li>
      `).join("")
    : "<li>No pending connection requests.</li>";
}

function renderPeople(people) {
  el.usersList.innerHTML = people.length
    ? people.map((person) => {
        const actionButton = person.incomingRequest
          ? `<button data-action="accept" data-id="${person.connectionId}">Accept request</button>`
          : person.relationship === "Not connected"
            ? `<button data-action="request" data-id="${person.id}">Connect</button>`
            : `<span class="badge muted">${person.relationship}</span>`;

        return `
          <li>
            <strong>${person.name}</strong><br>
            <span>${person.headline}</span><br>
            <small>${person.email}</small><br>
            ${actionButton}
          </li>
        `;
      }).join("")
    : "<li>No people found.</li>";
}

function renderConnections(connections) {
  el.connectionsList.innerHTML = connections.length
    ? connections.map((connection) => `
        <li>
          <button class="secondary connection-button" data-chat="${connection.id}" data-name="${connection.name}">
            <strong>${connection.name}</strong><br>
            <span>${connection.headline}</span>
          </button>
        </li>
      `).join("")
    : "<li>No accepted connections yet.</li>";

  if (!connections.some((connection) => connection.id === activeChatUserId)) {
    activeChatUserId = connections[0]?.id ?? null;
  }

  if (activeChatUserId) {
    void loadMessages();
  } else {
    el.chatWith.textContent = "Messages";
    el.messageList.innerHTML = "<li>Connect with someone to start messaging.</li>";
  }
}

function setSignedInState(isSignedIn) {
  el.authCard.classList.toggle("hidden", isSignedIn);
  el.profileCard.classList.toggle("hidden", !isSignedIn);
  el.pendingCard.classList.toggle("hidden", !isSignedIn);
  el.feedArea.classList.toggle("hidden", !isSignedIn);
  el.socialArea.classList.toggle("hidden", !isSignedIn);
  el.logoutBtn.classList.toggle("hidden", !isSignedIn);
}

function setNotice(message) {
  el.authStatus.textContent = message;
}

function clearSession() {
  token = "";
  activeChatUserId = null;
  localStorage.removeItem(tokenKey);
  clearInterval(refreshTimer);
  refreshTimer = null;
  setSignedInState(false);
  el.chatWith.textContent = "Messages";
  el.messageList.innerHTML = "<li>Select a connection to open a conversation.</li>";
}

function setupMessageRefresh() {
  clearInterval(refreshTimer);
  refreshTimer = setInterval(() => {
    if (activeChatUserId) {
      void loadMessages();
    }
  }, 15000);
}

async function api(url, options = {}, requiresAuth = true) {
  const headers = {
    "Content-Type": "application/json",
    ...(options.headers || {})
  };

  if (requiresAuth && token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(url, { ...options, headers });
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}
