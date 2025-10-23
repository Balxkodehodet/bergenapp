import { createContext, useState } from "react";

export const AppContext = createContext();

export function AppProvider({ children }) {

   
    const {weatherData, setWeatherData} = useState(null);

     return (
    <AppContext.Provider
      value={{weatherData, setWeatherData}}>
        {children}
      </AppContext.Provider>
}