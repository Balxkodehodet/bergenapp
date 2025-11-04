# React + Vite

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Babel](https://babeljs.io/) (or [oxc](https://oxc.rs) when used in [rolldown-vite](https://vite.dev/guide/rolldown)) for Fast Refresh
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/) for Fast Refresh

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the ESLint configuration

If you are developing a production application, we recommend using TypeScript with type-aware lint rules enabled. Check out the [TS template](https://github.com/vitejs/vite/tree/main/packages/create-vite/template-react-ts) for information on how to integrate TypeScript and [`typescript-eslint`](https://typescript-eslint.io) in your project.

# Bergen App

Bergen city information app with React frontend and .NET API backend.

## Frontend Setup (React + Vite)

```bash
npm install
npm run dev
```
Runs on `http://localhost:5173`

## Backend Setup (.NET 8 API)

### Prerequisites
- .NET 8 SDK
- SQL Server (SSMS recommended)

### Quick Start

1. **Configure Database**
   
   Update `backend/BergenCollectionApi/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "StopsDb": "Server=YOUR_SERVER_NAME;Database=EnturStopsDB;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```
   Replace `YOUR_SERVER_NAME` with your SQL Server instance (e.g., `localhost`, `.\SQLEXPRESS`, etc.)

2. **Run API**
   ```bash
   cd backend/BergenCollectionApi
   dotnet run
   ```
   
   API runs on `http://localhost:5049`

### What Happens on First Run
- Database and tables are created automatically
- Bus stop data is downloaded from Entur and imported
- Takes ~2 minutes on first startup

### API Endpoints
- `GET /api/bus-departures-by-name?stopName={name}` - Bus departures by stop name
- `GET /api/bike-stations` - Bergen bike stations
- `GET /api/bergen-temp` - Bergen weather

### Troubleshooting
- **Database connection issues**: Verify SQL Server is running and connection string is correct
- **Port conflicts**: Change port in `appsettings.json`
- **GTFS download fails**: Check internet connection, retry startup