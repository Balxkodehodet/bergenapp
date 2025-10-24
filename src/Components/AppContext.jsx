import { createContext, useState } from "react";

export const AppContext = createContext();

export function AppProvider({ children }) {
  const [weatherData, setWeatherData] = useState([]);
  const [sykkelData, setSykkelData] = useState([]);

  return (
    <AppContext.Provider
      value={{ weatherData, setWeatherData, sykkelData, setSykkelData }}
    >
      {children}
    </AppContext.Provider>
  );
}
