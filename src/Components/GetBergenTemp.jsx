import { useEffect, useContext } from "react";
import { AppContext } from "./AppContext.jsx";

const { weatherData, setWeatherData } = useContext(AppContext);

export default function GetBergenTemp() {
  useEffect(() => {
    async function fetchTemp() {
      const res = await fetch("http://localhost:3000/api/bergen-temp");
      const data = await res.json();
      console.log(
        "Bergen temperature data:",
        data.properties.timeseries[0].data.instant.details.air_temperature,
        data.properties.timeseries[1].data.instant.details.air_temperature,
        data.properties.timeseries[2].data.instant.details.air_temperature
      );
    }
    fetchTemp();
  }, []);

  return (
    <>
      {!data && <p>Laster data...</p>}
      {data && (
        <div>
          <p>Nåværende temperatur i Bergen: {data.currentTemp}°C</p>
          <p>Temperatur om 1 time: {data.tempIn1Hour}°C</p>
          <p>Temperatur om 2 timer: {data.tempIn2Hours}°C</p>
        </div>
      )}
    </>
  );
}
