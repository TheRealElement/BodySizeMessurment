# BodySize Web Client (static)

This is a **static** web front‑end that talks to the existing **BodySize.Server** (`/api/Analyze/analyze`).
Open `index.html` directly or serve the folder via any static server.

## Use
1. Start your server (ASP.NET Core) at `http://localhost:5189` (or change the URL at the bottom of the page).
2. Open `index.html` in a browser.
3. Select Front/Back/Left/Right photos, set height and gender, click **Analyze**.
4. Results appear on the left; 3D mannequin + rings on the right.

## Mannequin
This demo shows a simple built-in mannequin (cylinders/sphere) so it works without extra assets.
To use a real human model:
- Put `mannequin.glb` into `assets/` and host with a GLTFLoader build of three.js, or
- Extend `loadGlb()` in `app.js` with `THREE.GLTFLoader` (from `three/examples/jsm/loaders/GLTFLoader.js`).

## CORS
If you open `index.html` from `file://`, your server must allow CORS from any origin during development:
```
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
app.UseCors();
```
