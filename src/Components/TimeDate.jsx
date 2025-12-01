import { useContext } from "react";
import { AppContext } from "./AppContext.jsx";

export default function TimeDate() {
  const { weatherData } = useContext(AppContext);
  return (
    <>
      {!weatherData ||
      !weatherData.properties ||
      !weatherData.properties.timeseries ? (
        <p className="loading">Laster data...</p>
      ) : (
        <h2 className="tid-header">
          Værdata vises for tid:<br></br>{" "}
          {weatherData.properties.timeseries[2].time
            .replace("T", " Tid: ")
            .replace("Z", " ")}
        </h2>
      )}
    </>
  );
}
