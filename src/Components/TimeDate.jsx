import { useContext } from "react";
import { AppContext } from "./AppContext.jsx";

export default function TimeDate() {
  const { weatherData } = useContext(AppContext);
  return (
    <>
      {!weatherData ||
      !weatherData.properties ||
      !weatherData.properties.timeseries ? (
        <p>Laster data...</p>
      ) : (
        <h2 className="tid-header">
          Vær sist oppdatert:<br></br>{" "}
          {weatherData.properties.timeseries[2].time
            .replace("T", " Tid: ")
            .replace("Z", " ")}
        </h2>
      )}
    </>
  );
}
