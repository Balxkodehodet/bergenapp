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

app.get("/api/bike-data", async (req, res) => {
  try {
    let stationStat =
      "https://gbfs.urbansharing.com/bergenbysykkel.no/station_status.json";
    let stationInfo =
      "https://gbfs.urbansharing.com/bergenbysykkel.no/station_information.json";

    let options = {
      headers: {
        "User-Agent": "balx042025@gmail.com", // MET Norway requires a User-Agent
      },
    };

    const response = await fetch(stationStat, options);
    const response2 = await fetch(stationInfo, options);
    const data = await response.json();
    const data2 = await response2.json();

    const merged = data.data.stations.map((station) => {
      const status =
        data2.data.stations.find((s) => {
          return s.station_id === station.station_id;
        }) || {};
      return { ...status, ...station };
    });
    res.json(merged);
  } catch (err) {
    console.error("Error fetching :", err);
    res.status(500).json({ error: "Failed to fetch" });
  }
});

const PORT = process.env.PORT || 3001;
app.listen(PORT, () =>
  console.log(`✅ Server running on http://localhost:${PORT}`)
);
