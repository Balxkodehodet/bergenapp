import { useState, useEffect } from "react";
import "../App.css";

export default function GetBusData() {
  const [busData, setBusData] = useState("");
  console.log("Bus data from context:", busData);
  useEffect(() => {
    async function fetchBusData() {
      const res = await fetch(
        "http://localhost:5173/api/bus-departures-by-name"
      );
      const data = await res.json();
      console.log("Data fetched of bus data", data);
      setBusData(data);
    }
    fetchBusData();
  }, []);
  let busdepartures = [];
  if (busData) {
    busdepartures = busData.data.stopPlace.estimatedCalls.filter(
      (transport) => {
        return (
          transport.serviceJourney.line.transportMode.toLowerCase() ===
          "BUS".toLowerCase()
        );
      }
    );
  }
  return (
    <>
      {!busData ? (
        <p>Laster data...</p>
      ) : (
        <>
          <div className="bus-data">
            <h2>{busData.data.stopPlace.name}</h2>
            {busdepartures.map((bus, index) => (
              <div key={index}>
                <p>
                  Buss til: <b>{bus.destinationDisplay.frontText}</b>
                  <br></br>
                  Forventet avgangstid:{" "}
                  <b>
                    {bus.expectedDepartureTime
                      .replace("T", " Kl:")
                      .replace("+01:00", " ")}
                  </b>
                  <br></br>
                  Linje:
                  <b>
                    {bus.serviceJourney.line.id.replace("SKY:Line:", "") + " "}
                  </b>
                </p>
                <hr></hr>
              </div>
            ))}
          </div>
        </>
      )}
    </>
  );
}
