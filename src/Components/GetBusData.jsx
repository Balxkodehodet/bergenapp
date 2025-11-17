import { useState, useEffect } from "react";
import "../App.css";

export default function GetBusData() {
  const [busData, setBusData] = useState("");
  const [stopName, setStopName] = useState("");
  const [bus, setBus] = useState("arna");
  console.log("Bus data from context:", busData);
  useEffect(() => {
    async function fetchBusData() {
      const res = await fetch(`http://localhost:3001/api/buss-data`);
      const data = await res.json();
      console.log("Data fetched of bus data", data);
      setBusData(data);
    }
    fetchBusData();
  }, []);
  let busdepartures = [];
  let busdepartures2;
  if (busData) {
    busdepartures = busData.data.stopPlace.estimatedCalls;
    busdepartures2 = busdepartures.filter(
      (bus) => bus.serviceJourney.line.transportMode === "BUS".toLowerCase()
    );
  }

  console.log("bus departures: ", busdepartures2);
  console.log(busData);
  console.log("BUS:", bus);
  return (
    <>
      {!busData ? (
        <p>Laster data...</p>
      ) : (
        <>
          <div className="bus-data">
            <h2>Neste 15 busser avgang fra Bergen buss stasjon: </h2>
            {busdepartures2.map((bus, index) => (
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
