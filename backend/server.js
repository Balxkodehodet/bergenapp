// server/server.js
import express from "express";
import fetch from "node-fetch";
import cors from "cors";
import dotenv from "dotenv";

dotenv.config();

const app = express();
app.use(cors()); // allow React frontend
app.use(express.json());

app.get("/api/bergen-temp", async (req, res) => {
  try {
    let url =
      "https://api.met.no/weatherapi/locationforecast/2.0/compact?lat=60.3913&lon=5.3221";

    let options = {
      headers: {
        "User-Agent": "balx042025@gmail.com", // MET Norway requires a User-Agent
      },
    };

    const response = await fetch(url, options);
    const data = await response.json();
    res.json(data);
  } catch (err) {
    console.error("Error fetching :", err);
    res.status(500).json({ error: "Failed to fetch" });
  }
});

const PORT = process.env.PORT || 3000;
app.listen(PORT, () =>
  console.log(`✅ Server running on http://localhost:${PORT}`)
);
