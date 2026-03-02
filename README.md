# CircleHub (ASP.NET Core + SQLite)

CircleHub is a social media app prototype with a **fully functional C# backend** and database persistence.

## Implemented backend capabilities

- User registration and sign-in
- Token-based authenticated API access
- Connection requests and acceptance flow
- Feed visibility limited to: you + accepted connections
- Create posts saved to SQLite
- Direct messaging between accepted connections

## Tech stack

- ASP.NET Core Minimal API (`net8.0`)
- Entity Framework Core + SQLite (`circlehub.db`)
- Static frontend served from `wwwroot`

## Run

```bash
dotnet restore
dotnet run
```

Open the app URL printed by `dotnet run`.

## API summary

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/me`
- `GET /api/users?q=...`
- `GET /api/connections`
- `POST /api/connections/request/{userId}`
- `POST /api/connections/accept/{connectionId}`
- `GET /api/feed`
- `POST /api/posts`
- `GET /api/messages/{userId}`
- `POST /api/messages/{userId}`

## Seed users

For local testing, the database seeds users with password `password123`:

- `alex@circlehub.dev`
- `sasha@circlehub.dev`
- `derek@circlehub.dev`
