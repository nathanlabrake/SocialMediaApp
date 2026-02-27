const STORAGE_KEY = "circlehub-data-v1";

const seedData = {
  suggestions: [
    { name: "Sasha Lee", role: "Frontend engineer" },
    { name: "Derek Shah", role: "Growth strategist" },
    { name: "Priya N.", role: "Community manager" }
  ],
  communities: ["Design Critique Club", "Remote Builders", "Sustainable Living"],
  events: ["Creator Meetup - Fri", "Product Workshop - Tue", "AI Ethics Panel - Sat"],
  trends: ["#BuildInPublic", "#RemoteWork", "#ClimateTech", "#CreatorEconomy"],
  messages: [],
  posts: [
    {
      id: crypto.randomUUID(),
      title: "Launching my side project",
      content: "After 3 months of work, I finally released an MVP. I'd love feedback from builders.",
      mood: "🎉 Excited",
      likes: 3,
      comments: ["Big milestone, congrats!", "Share the link 👀"],
      time: new Date().toISOString()
    }
  ],
  theme: "light",
  connections: 128
};

const state = load();
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
  connectionCount: document.querySelector("#connectionCount")
};

renderAll();
attachEvents();

function load() {
  const data = localStorage.getItem(STORAGE_KEY);
  if (!data) return structuredClone(seedData);
  return { ...structuredClone(seedData), ...JSON.parse(data) };
}

function persist() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}

function renderAll() {
  document.body.classList.toggle("dark", state.theme === "dark");
  renderList(el.suggestionList, state.suggestions.map((s, index) => {
    const connected = s.connected ? "Connected" : `<button data-connect="${index}" class="secondary">Connect</button>`;
    return `<li><strong>${s.name}</strong><br><span>${s.role}</span><br>${connected}</li>`;
  }));

  renderList(el.communityList, state.communities.map((name) => `<li>• ${name}</li>`));
  renderList(el.eventList, state.events.map((event) => `<li>${event}</li>`));
  renderList(el.trendList, state.trends.map((tag) => `<li>${tag}</li>`));
  renderList(el.messageLog, state.messages.map((m) => `<li><strong>To ${m.to}:</strong> ${m.text}</li>`));
  el.connectionCount.textContent = state.connections;
  renderFeed();
}

function renderList(target, items) {
  target.innerHTML = items.join("");
}

function renderFeed(filter = "") {
  el.feedList.innerHTML = "";
  const posts = [...state.posts]
    .sort((a, b) => new Date(b.time) - new Date(a.time))
    .filter((post) => [post.title, post.content, post.mood].join(" ").toLowerCase().includes(filter.toLowerCase()));

  posts.forEach((post) => {
    const node = el.postTemplate.content.firstElementChild.cloneNode(true);
    node.dataset.postId = post.id;
    node.querySelector(".post-title").textContent = `${post.mood} ${post.title}`;
    node.querySelector(".post-meta").textContent = new Date(post.time).toLocaleString();
    node.querySelector(".post-content").textContent = post.content;

    const likeBtn = node.querySelector(".like-btn");
    likeBtn.textContent = `Like (${post.likes})`;
    likeBtn.addEventListener("click", () => {
      post.likes += 1;
      persist();
      renderFeed(el.searchInput.value);
    });

    const commentBtn = node.querySelector(".comment-btn");
    const commentArea = node.querySelector(".comment-area");
    commentBtn.addEventListener("click", () => commentArea.classList.toggle("hidden"));

    const commentList = node.querySelector(".comment-list");
    renderList(commentList, post.comments.map((comment) => `<li>💬 ${comment}</li>`));

    const commentForm = node.querySelector(".comment-form");
    commentForm.addEventListener("submit", (event) => {
      event.preventDefault();
      const input = commentForm.querySelector(".comment-input");
      post.comments.unshift(input.value.trim());
      input.value = "";
      persist();
      renderFeed(el.searchInput.value);
    });

    el.feedList.append(node);
  });
}

function attachEvents() {
  el.postForm.addEventListener("submit", (event) => {
    event.preventDefault();
    state.posts.push({
      id: crypto.randomUUID(),
      title: el.postTitle.value.trim(),
      content: el.postContent.value.trim(),
      mood: el.postMood.value,
      likes: 0,
      comments: [],
      time: new Date().toISOString()
    });
    el.postForm.reset();
    persist();
    renderFeed(el.searchInput.value);
  });

  el.suggestionList.addEventListener("click", (event) => {
    const index = event.target.dataset.connect;
    if (index === undefined) return;
    if (!state.suggestions[index].connected) {
      state.suggestions[index].connected = true;
      state.connections += 1;
      persist();
      renderAll();
    }
  });

  el.messageForm.addEventListener("submit", (event) => {
    event.preventDefault();
    state.messages.unshift({
      to: el.messageTo.value.trim(),
      text: el.messageText.value.trim()
    });
    el.messageForm.reset();
    persist();
    renderAll();
  });

  el.searchInput.addEventListener("input", (event) => {
    renderFeed(event.target.value);
  });

  el.themeToggle.addEventListener("click", () => {
    state.theme = state.theme === "light" ? "dark" : "light";
    persist();
    renderAll();
  });
}
