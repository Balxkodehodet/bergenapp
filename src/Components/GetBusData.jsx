import { useState, useEffect } from "react";
import "../App.css";

export default function GetBusData() {
  const [busData, setBusData] = useState(null);
  const [bus, setBus] = useState("Bergen busstasjon");

  useEffect(() => {
    async function fetchBusData() {
      if (!bus || bus.trim() === "") {
        // don't fetch for empty search; clear results
        setBusData(null);
        return;
      }

      try {
        const apiBase = import.meta.env.VITE_API_BASE || "http://localhost:5049";
        const url = `${apiBase}/api/bus-departures-by-name?stopName=${encodeURIComponent(
          bus
        )}`;
        const res = await fetch(url);
        if (!res.ok) {
          console.error("Fetch failed:", res.status, res.statusText);
          setBusData(null);
          return;
        }
        const data = await res.json();
        console.log("Data fetched of bus data", data);
        setBusData(data);
      } catch (err) {
        console.error("Error fetching bus data:", err);
        setBusData(null);
      }
    }

    fetchBusData();
  }, [bus]);

  let busdepartures2 = [];
  if (busData && busData.data && Array.isArray(busData.data.estimatedCalls)) {
    const busdepartures = busData.data.estimatedCalls;
    busdepartures2 = busdepartures.filter((b) => {
      return (
        (b.serviceJourney?.line?.transportMode || "").toLowerCase() === "bus"
      );
    });
  }

  return (
    <>
      <form
        className="search-bus"
        onSubmit={(e) => {
          e.preventDefault();
          // submit doesn't need to change state here; bus is already bound to input
          setBus(bus);
        }}
      >
        <label>
          <strong>Søk etter buss stasjon i vestland her:</strong>
          <input
            type="search"
            value={bus}
            onChange={(e) => setBus(e.target.value)}
          />
        </label>
      </form>

      {!bus || bus.trim() === "" ? (
        <p className="søkeData">Skriv inn et stoppnavn for å søke.</p>
      ) : busData === null ? (
        <p className="søkeData">Laster data...</p>
      ) : busdepartures2.length === 0 ? (
        <p className="søkeData">Ingen avganger funnet for «{bus}».</p>
      ) : (
        <div className="bus-data">
          <h2>Neste 15 busser avgang fra {bus}:</h2>
          {busdepartures2.map((b, index) => (
            <div key={index}>
              <p>
                Buss til: <b>{b.destinationDisplay?.frontText}</b>
                <br />
                Forventet avgangstid:{" "}
                <b>
                  {b.expectedDepartureTime
                    ?.replace("T", " Kl:")
                    .replace("+01:00", " ")}
                </b>
                <br />
                Linje:{" "}
                <b>{b.serviceJourney?.line?.id?.replace("SKY:Line:", "")}</b>
              </p>
              <hr />
            </div>
          ))}
        </div>
      )}
    </>
  );
}
