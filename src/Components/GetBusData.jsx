import { useState, useEffect } from "react";
import "../App.css";

export default function GetBusData() {
  const [busData, setBusData] = useState("");
  console.log("Bus data from context:", busData);
  useEffect(() => {
    async function fetchBusData() {
      const res = await fetch("http://localhost:3001/api/buss-data");
      const data = await res.json();
      console.log("Data fetched of bus data", data);
      setBusData(data);
    }
    fetchBusData();
  }, []);

  return <>{!busData ? <p>Laster data...</p> : <></>}</>;
}
