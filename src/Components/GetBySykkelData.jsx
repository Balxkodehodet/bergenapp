import { useEffect, useContext } from "react";
import { AppContext } from "./AppContext.jsx";
import "../App.css";

export default function GetBergenTemp() {
  const { sykkelData, setSykkelData } = useContext(AppContext);
  let data;
  useEffect(() => {
    async function fetchBikeData() {
      const res = await fetch("http://localhost:3001/api/bike-data");
      const data = await res.json();
      setSykkelData(data);
    }
    fetchBikeData();
  }, []);

  let address = "";
  let numBikesAvailable = 0;
  let numDocksAvailable = 0;
  let name = "";
  let x_street = "";

  useEffect(() => {
    let randomNr;
    setInterval(() => {
      randomNr = Math.floor(Math.random() * sykkelData.length);
      console.log("randomNr: ", randomNr);
    }, 10000);
    address = data[randomNr].address;
    numBikesAvailable = data[randomNr].num_bikes_available;
    numDocksAvailable = data[randomNr].num_docks_available;
    name = data[randomNr].name;
    x_street = data[randomNr].x_street;
  }, []);

  console.log("addresse: ", address);
  console.log("navn: ", name);
  console.log("x_street: ", x_street);
  console.log("numBikesAvailable: ", numBikesAvailable);
  console.log("numDocksAvailable: ", numDocksAvailable);
  return (
    <>
      {!sykkelData ? (
        <p>Laster data...</p>
      ) : (
        <>
          <div className="bike-data">
            <p>
              Navn: <strong>{name}</strong>
              <br></br>
              Addresse: <strong>{address}</strong>
              <br></br>
              Antall sykler tilgjengelig: <strong>{numBikesAvailable}</strong>
              <br></br>
              Antall oppbevaringsplasser tilgjengelig:{" "}
              <strong>{numDocksAvailable}</strong>
              <br></br>
              x_street: <strong>{x_street}</strong>
            </p>
          </div>
        </>
      )}
    </>
  );
}
