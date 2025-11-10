import { useState, useEffect } from "react";
import "../App.css";

export default function GetBusData() {
  const [busData, setBusData] = useState("");
  const [stopName, setStopName] = useState("Bergen");
  const [bus, setBus] = useState("Bergen");
  console.log("Bus data from context:", busData);
  useEffect(() => {
    async function fetchBusData() {
      const res = await fetch(
        `http://localhost:5049/api/bus-departures-by-name?stopName=Bergen`
      );
      const data = await res.json();
      console.log("Data fetched of bus data", data);
      setBusData(data);
    }
    fetchBusData();
  }, []);
  let busdepartures = [];
  let uniqueStops = [];
  if (busData) {
    busdepartures = busData.departures;
    // Create a unique array of stops by stopName
    uniqueStops = Array.from(
      new Map(busdepartures.map((item) => [item.stopName, item])).values()
    );
  }

  async function handleSubmit(event) {
    event.preventDefault();
    console.log("Form submitted, selected option:", event.target.value);
    const bus = await busData.departures.find(
      (station) => station.stopName === event.target.value
    );
    setBus(bus);
  }

  console.log(busdepartures);
  console.log(busData);
  console.log("BUS:", bus);
  return (
    <>
      {!busData ? (
        <p>Laster data...</p>
      ) : (
        <>
          <div className="bus-formdata">
            <h2>Buss stopper:</h2>
            <form onSubmit={handleSubmit}>
              <label>
                Velg
                <select value={bus} onChange={(e) => setBus(e.target.value)}>
                  <option value="">Velg et stopp</option>
                  {uniqueStops.map((d, index) => (
                    <option key={d.stopId + index} value={d.stopName}>
                      {d.stopName}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Søk etter buss stopp:
                <input
                  type="search"
                  value={stopName}
                  onChange={(e) => setStopName(e.target.value)}
                />
                {stopName &&
                  uniqueStops.map((d, index) => (
                    <option key={d.stopId + index} value={d.stopName}>
                      {d.stopName}
                    </option>
                  ))}
              </label>
              <button type="submit">Søk</button>
            </form>
          </div>
          <div className="bus-data">
            <h2>Velg</h2>
            {busdepartures.map((bus, index) => (
              <div key={index}>
                <p>
                  Buss til: <b>{bus.departure.destinationDisplay.frontText}</b>
                  <br></br>
                  Forventet avgangstid:{" "}
                  <b>
                    {bus.departure.expectedDepartureTime
                      .replace("T", " Kl:")
                      .replace("+01:00", " ")}
                  </b>
                  <br></br>
                  Linje:
                  <b>
                    {bus.departure.serviceJourney.line.id.replace(
                      "SKY:Line:",
                      ""
                    ) + " "}
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
