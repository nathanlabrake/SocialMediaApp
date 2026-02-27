const el = {
  feedList: document.querySelector("#feedList"),
  postForm: document.querySelector("#postForm"),
  postTitle: document.querySelector("#postTitle"),
  postContent: document.querySelector("#postContent"),
  postMood: document.querySelector("#postMood"),
  postTemplate: document.querySelector("#postTemplate"),
  suggestionList: document.querySelector("#suggestionList"),
  communityList: document.querySelector("#communityList"),
  eventList: document.querySelector("#eventList"),
  trendList: document.querySelector("#trendList"),
  messageForm: document.querySelector("#messageForm"),
  messageTo: document.querySelector("#messageTo"),
  messageText: document.querySelector("#messageText"),
  messageLog: document.querySelector("#messageLog"),
  searchInput: document.querySelector("#searchInput"),
  themeToggle: document.querySelector("#themeToggle"),
  profileHeadline: document.querySelector("#profileHeadline"),
  profileName: document.querySelector("#profileName"),
  connectionCount: document.querySelector("#connectionCount")
};

const fallback = {
  profile: { name: "Alex Morgan", headline: "Product designer • Austin", connectionCount: 128 },
  suggestions: [{ id: 1, name: "Sasha Lee", role: "Frontend engineer", connected: false }],
  communities: ["Design Critique Club"],
  events: ["Creator Meetup - Fri"],
  trends: ["#BuildInPublic"],
  messages: [],
  posts: [{ id: 1, title: "Backend unavailable", content: "Run `dotnet run` to enable real DB-backed data.", mood: "⚠️", likes: 0, time: new Date().toISOString(), comments: [] }]
};

let theme = localStorage.getItem("circlehub-theme") || "light";
let offlineMode = false;
init();

async function init() {
  document.body.classList.toggle("dark", theme === "dark");
  try {
    await renderSidebar();
    await renderFeed();
  } catch {
    offlineMode = true;
    renderFallback();
  }
  attachEvents();
}

function renderFallback() {
  el.profileName.textContent = fallback.profile.name;
  el.profileHeadline.textContent = fallback.profile.headline;
  el.connectionCount.textContent = fallback.profile.connectionCount;
  renderList(el.suggestionList, fallback.suggestions.map((s) => `<li><strong>${s.name}</strong><br><span>${s.role}</span></li>`));
  renderList(el.communityList, fallback.communities.map((c) => `<li>• ${c}</li>`));
  renderList(el.eventList, fallback.events.map((event) => `<li>${event}</li>`));
  renderList(el.trendList, fallback.trends.map((tag) => `<li>${tag}</li>`));
  renderList(el.messageLog, ["<li>Start backend to enable messaging.</li>"]);
  renderStaticPosts(fallback.posts);
}

async function renderSidebar() {
  const data = await api("/api/bootstrap");
  el.profileName.textContent = data.profile.name;
  el.profileHeadline.textContent = data.profile.headline;
  el.connectionCount.textContent = data.profile.connectionCount;

  renderList(el.suggestionList, data.suggestions.map((s) =>
    `<li><strong>${s.name}</strong><br><span>${s.role}</span><br>${s.connected ? "Connected" : `<button data-connect="${s.id}" class="secondary">Connect</button>`}</li>`
  ));
  renderList(el.communityList, data.communities.map((c) => `<li>• ${c}</li>`));
  renderList(el.eventList, data.events.map((event) => `<li>${event}</li>`));
  renderList(el.trendList, data.trends.map((tag) => `<li>${tag}</li>`));
  renderList(el.messageLog, data.messages.map((m) => `<li><strong>To ${m.to}:</strong> ${m.text}</li>`));
}

async function renderFeed() {
  const q = encodeURIComponent(el.searchInput.value || "");
  const posts = await api(`/api/posts?q=${q}`);
  renderStaticPosts(posts);
}

function renderStaticPosts(posts) {
  el.feedList.innerHTML = "";
  posts.forEach((post) => {
    const node = el.postTemplate.content.firstElementChild.cloneNode(true);
    node.querySelector(".post-title").textContent = `${post.mood} ${post.title}`;
    node.querySelector(".post-meta").textContent = new Date(post.time).toLocaleString();
    node.querySelector(".post-content").textContent = post.content;
    const likeBtn = node.querySelector(".like-btn");
    likeBtn.textContent = `Like (${post.likes})`;

    if (!offlineMode) {
      likeBtn.addEventListener("click", async () => {
        await api(`/api/posts/${post.id}/like`, { method: "POST" });
        await renderFeed();
      });
    }

    const commentArea = node.querySelector(".comment-area");
    node.querySelector(".comment-btn").addEventListener("click", () => commentArea.classList.toggle("hidden"));
    renderList(node.querySelector(".comment-list"), post.comments.map((c) => `<li>💬 ${c}</li>`));

    if (!offlineMode) {
      node.querySelector(".comment-form").addEventListener("submit", async (event) => {
        event.preventDefault();
        const input = node.querySelector(".comment-input");
        await api(`/api/posts/${post.id}/comments`, { method: "POST", body: JSON.stringify({ content: input.value }) });
        input.value = "";
        await renderFeed();
      });
    }

    el.feedList.append(node);
  });
}

function attachEvents() {
  el.postForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (offlineMode) return;
    await api("/api/posts", { method: "POST", body: JSON.stringify({ title: el.postTitle.value, content: el.postContent.value, mood: el.postMood.value }) });
    el.postForm.reset();
    await renderFeed();
  });

  el.suggestionList.addEventListener("click", async (event) => {
    if (offlineMode) return;
    const id = event.target.dataset.connect;
    if (!id) return;
    await api(`/api/suggestions/${id}/connect`, { method: "POST" });
    await renderSidebar();
  });

  el.messageForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (offlineMode) return;
    await api("/api/messages", { method: "POST", body: JSON.stringify({ to: el.messageTo.value, text: el.messageText.value }) });
    el.messageForm.reset();
    await renderSidebar();
  });

  el.searchInput.addEventListener("input", () => { if (!offlineMode) renderFeed(); });
  el.themeToggle.addEventListener("click", () => {
    theme = theme === "light" ? "dark" : "light";
    localStorage.setItem("circlehub-theme", theme);
    document.body.classList.toggle("dark", theme === "dark");
  });
}

function renderList(target, items) {
  target.innerHTML = items.join("");
}

async function api(url, options = {}) {
  const response = await fetch(url, { headers: { "Content-Type": "application/json" }, ...options });
  if (!response.ok) throw new Error(`API error: ${response.status}`);
  if (response.status === 204) return null;
  return response.json();
}
