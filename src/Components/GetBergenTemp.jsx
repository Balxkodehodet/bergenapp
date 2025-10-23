import { useEffect, useContext } from "react";
import { AppContext } from "./AppContext.jsx";
import "../App.css";

export default function GetBergenTemp() {
  const { weatherData, setWeatherData } = useContext(AppContext);
  console.log("Weather data from context:", weatherData);
  useEffect(() => {
    async function fetchTemp() {
      const res = await fetch("http://localhost:3000/api/bergen-temp");
      const data = await res.json();
      console.log(
        "Bergen temperature data:",
        data,
        data.properties.timeseries[0].data.instant.details.air_temperature,
        data.properties.timeseries[1].data.instant.details.air_temperature,
        data.properties.timeseries[2].data.instant.details.air_temperature
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
        <p>Laster data...</p>
      ) : (
        <>
          <div className="weather-data">
            <p>
              Temp idag i Bergen:{" "}
              <strong>
                {
                  weatherData.properties.timeseries[2].data.instant.details
                    .air_temperature
                }
                °C
              </strong>
              <br></br> Om 1 time:{" "}
              <strong>
                {
                  weatherData.properties.timeseries[3].data.instant.details
                    .air_temperature
                }
                °C
              </strong>
              <br></br> Om 2 timer:{" "}
              <strong>
                {
                  weatherData.properties.timeseries[4].data.instant.details
                    .air_temperature
                }
                °C
              </strong>
            </p>
          </div>
        </>
      )}
    </>
  );
}
