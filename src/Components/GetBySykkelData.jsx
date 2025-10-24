import { useEffect, useContext, useState } from "react";
import { AppContext } from "./AppContext.jsx";
import "../App.css";

export default function GetBergenTemp() {
  const { sykkelData, setSykkelData } = useContext(AppContext);
  const [randomStation, setRandomStation] = useState(null);
  let data;
  useEffect(() => {
    async function fetchBikeData() {
      const res = await fetch("http://localhost:3001/api/bike-data");
      const data = await res.json();
      setSykkelData(data); // save full array in context

      // pick initial random station
      setRandomStation(data[Math.floor(Math.random() * data.length)]);

      // start interval to update random station
      const interval = setInterval(() => {
        const randomNr = Math.floor(Math.random() * data.length);
        console.log("Random number:", randomNr);
        setRandomStation(data[randomNr]);
      }, 30000);

      return () => clearInterval(interval);
    }

    fetchBikeData();
  }, []);

  return (
    <>
      {!randomStation ? (
        <p>Laster data...</p>
      ) : (
        <>
          <div className="bike-data">
            <p>
              {randomStation.num_docks_available > 0
                ? "plasser med mer enn 0 sykler:"
                : "Plasser med ingen ledige sykler"}
              <br></br>
              Navn: <strong>{randomStation.name}</strong>
              <br></br>
              Addresse: <strong>{randomStation.address}</strong>
              <br></br>
              Antall sykler tilgjengelig:{" "}
              <strong>{randomStation.num_bikes_available}</strong>
              <br></br>
              Antall oppbevaringsplasser tilgjengelig:{" "}
              <strong>{randomStation.num_docks_available}</strong>
              <br></br>
              x_street: <strong>{randomStation.cross_street}</strong>
            </p>
          </div>
        </>
      )}
    </>
  );
}
