# CircleHub (C# + SQLite)

CircleHub is a Facebook-like (but not Facebook-copy) social media web app built with **ASP.NET Core (C#)** and **SQLite**.

## What changed

This version is no longer front-end-only. Posts, comments, likes, connection actions, and messages are persisted in a database via backend APIs.

## Stack

- ASP.NET Core Minimal API (`net8.0`)
- Entity Framework Core + SQLite
- HTML/CSS/Vanilla JS frontend served from `wwwroot`

## Features

- Create posts with mood tags
- Like posts and add comments
- Send quick messages
- Connect with suggested people (connection count updates)
- Search/filter posts
- Communities, events, and trends side panels
- Light/dark mode (theme preference saved locally)
- Data persisted in `circlehub.db`

## Run locally

```bash
dotnet restore
dotnet run
```

Then open the URL shown in the console (typically `http://localhost:5000` or `https://localhost:5001`).

## API overview

- `GET /api/bootstrap` – profile + sidebar data + recent messages
- `GET /api/posts?q=...` – fetch posts (with optional search)
- `POST /api/posts` – create post
- `POST /api/posts/{id}/like` – like post
- `POST /api/posts/{id}/comments` – add comment
- `POST /api/suggestions/{id}/connect` – connect with suggested user
- `POST /api/messages` – send message
