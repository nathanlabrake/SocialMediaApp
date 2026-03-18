# CircleHub (ASP.NET Core + SQLite)

CircleHub is a Facebook-style social network prototype built with **ASP.NET Core** and **SQLite**, with a real backend for authentication, connection management, private feed visibility, and direct messaging.

## Important repo note

I checked this repository before making changes and there is currently **no local `main` branch and no configured git remote**, so there was nothing available to pull or merge from `main` in this environment. The latest visible branch history only contained the `work` branch. This implementation was applied directly on top of the current branch state.

## What this app now supports

- User registration and sign-in
- Session token authentication using `Authorization: Bearer <token>`
- User discovery/search
- Connection requests, accept, and decline actions
- Feed visibility restricted to **you + accepted connections only**
- Post creation persisted in SQLite
- Direct messages restricted to accepted connections
- API discovery endpoint at `GET /api`
- Seed data for local demo users and sample conversations

## Tech stack

- ASP.NET Core Minimal API (`net8.0`)
- Entity Framework Core + SQLite (`circlehub.db`)
- Static frontend in `wwwroot`

## Run locally

```bash
dotnet restore
dotnet run
```

Then open the application URL printed by ASP.NET Core.

## Primary API endpoints

- `GET /api`
- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/me`
- `GET /api/bootstrap`
- `GET /api/users?q=...`
- `POST /api/connections/request/{userId}`
- `POST /api/connections/{connectionId}/accept`
- `POST /api/connections/{connectionId}/decline`
- `GET /api/feed?q=...`
- `POST /api/posts`
- `GET /api/messages/{userId}`
- `POST /api/messages/{userId}`

## Seed users

Use password `password123` for each of these:

- `alex@circlehub.dev`
- `sasha@circlehub.dev`
- `derek@circlehub.dev`
- `priya@circlehub.dev`
