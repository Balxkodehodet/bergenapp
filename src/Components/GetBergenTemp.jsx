import { useEffect, useContext } from "react";
import { AppContext } from "./AppContext.jsx";
import "../App.css";

export default function GetBergenTemp() {
  const { weatherData, setWeatherData } = useContext(AppContext);
  console.log("Weather data from context:", weatherData);
  useEffect(() => {
    async function fetchTemp() {
      const apiBase = import.meta.env.VITE_API_BASE || "http://localhost:5049";
      const res = await fetch(`${apiBase}/api/bergen-temp`);
      const data = await res.json();
      console.log(
        "Bergen temperature data:",
        data,
        data.properties.timeseries[2].data.instant.details.air_temperature,
        data.properties.timeseries[3].data.instant.details.air_temperature,
        data.properties.timeseries[4].data.instant.details.air_temperature
      );
      setWeatherData(data);
    }
    fetchTemp();
  }, []);

  return (
    <>
      {!weatherData ||
      !weatherData.properties ||
      !weatherData.properties.timeseries ? (
        <p className="loading">Laster data...</p>
      ) : (
        <>
          <div className="weather-data">
            <p>
              Temp idag i Bergen:{" "}
              <b>
                {
                  weatherData.properties.timeseries[2].data.instant.details
                    .air_temperature
                }
                °C
              </b>
            </p>

            <p>
              {" "}
              Om 1 time:{" "}
              <b>
                {
                  weatherData.properties.timeseries[3].data.instant.details
                    .air_temperature
                }
                °C
              </b>
            </p>

            <p>
              {" "}
              Om 2 timer:{" "}
              <b>
                {
                  weatherData.properties.timeseries[4].data.instant.details
                    .air_temperature
                }
                °C
              </b>
            </p>
            <h1 className="overskrift">Bergen App</h1>
          </div>
        </>
      )}
    </>
  );
}
