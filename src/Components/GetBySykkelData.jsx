import { useEffect, useContext, useState } from "react";
import { AppContext } from "./AppContext.jsx";
import "../App.css";

export default function GetBergenTemp() {
  const { sykkelData, setSykkelData } = useContext(AppContext);
  const [randomStation, setRandomStation] = useState(null);
  const [selectedOption, setSelectedOption] = useState("");
  const [interval, setInterval] = useState(null);

  useEffect(() => {
    async function fetchBikeData() {
      const apiBase = import.meta.env.VITE_API_BASE || "http://localhost:5049";
      const res = await fetch(`${apiBase}/api/bike-data`);
      const data = await res.json();
      setSykkelData(data); // save full array in context

      // pick initial random station
      setRandomStation(data[Math.floor(Math.random() * data.length)]);

      // start interval to update random station
      setInterval(
        setInterval(() => {
          const randomNr = Math.floor(Math.random() * data.length);
          console.log("Random number:", randomNr);
          setRandomStation(data[randomNr]);
        }, 15000)
      );

      return () => clearInterval(interval);
    }

    fetchBikeData();
  }, []);

  function handleZeroBikes() {
    clearInterval(interval);
    const filteredStations = sykkelData.filter(
      (station) =>
        station.num_bikes_available === 0 && station.num_docks_available > 0
    );
    const randomNr = Math.floor(Math.random() * filteredStations.length);
    setRandomStation(filteredStations[randomNr]);
    console.log(
      "Filtered  zero bikes but above 0 available dock stations:",
      filteredStations
    );
  }
  function handleBikes() {
    clearInterval(interval);
    const filteredStations = sykkelData.filter(
      (station) => station.num_bikes_available > 0
    );
    const randomNr = Math.floor(Math.random() * filteredStations.length);
    setRandomStation(filteredStations[randomNr]);
    console.log(
      "Filtered more than 0 bikes available stations:",
      filteredStations
    );
  }
  function handleRandomBikes() {
    const randomNr = Math.floor(Math.random() * sykkelData.length);
    console.log("Random number:", randomNr);
    setRandomStation(sykkelData[randomNr]);
  }
  function handleSubmit(event) {
    event.preventDefault();
    clearInterval(interval);
    console.log("Form submitted, selected option:", event.target[0].value);
    const selectedStation = sykkelData.find(
      (station) => station.name === event.target[0].value
    );
    setRandomStation(selectedStation);
    console.log("Selected station:", selectedStation);
  }
  return (
    <>
      {!randomStation ? (
        <p className="loading">Laster data...</p>
      ) : (
        <>
          <div className="bike-formdata">
            <h2>Bergen bysykler:</h2>
            <form onSubmit={handleSubmit}>
              <label>
                Velg
                <select>
                  {sykkelData.map((station) => (
                    <option key={station.station_id} value={station.name}>
                      {station.name}
                    </option>
                  ))}
                </select>
              </label>
              <button type="submit">Søk</button>
            </form>
            <button type="button" onClick={handleBikes}>
              Plasser med over 0 sykler
            </button>
            <button type="button" onClick={handleZeroBikes}>
              Tilgjengelige plasser
            </button>
            <button type="button" onClick={handleRandomBikes}>
              Tilfeldig stasjon
            </button>
            <br></br>
          </div>
          <div className="bike-data">
            <p>
              Navn: <strong>{randomStation.name}</strong>
            </p>
            <p>
              Addresse: <strong>{randomStation.address}</strong>
            </p>
            <p>
              Antall sykler tilgjengelig:{" "}
              <strong>{randomStation.num_bikes_available}</strong>
            </p>
            <p>
              Antall oppbevaringsplasser tilgjengelig:{" "}
              <strong>{randomStation.num_docks_available}</strong>
            </p>
            <p>
              x_street: <strong>{randomStation.cross_street}</strong>
            </p>
          </div>
        </>
      )}
    </>
  );
}
