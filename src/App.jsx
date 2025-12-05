import { useState } from "react";
import "./App.css";
import Footer from "./Components/footer.jsx";
import GetBergenTemp from "./Components/GetBergenTemp.jsx";
import { AppProvider } from "./Components/AppContext";
import TimeDate from "./Components/TimeDate.jsx";
import GetBySykkelData from "./Components/GetBySykkelData.jsx";
import GetBusData from "./Components/GetBusData.jsx";
import useTheme from "./Components/useTheme.jsx";

function App() {
  const [theme, setTheme] = useTheme();
  return (
    <AppProvider>
      <>
        <GetBergenTemp />
        <TimeDate />
        <button onClick={() => setTheme("light")}>Set light Theme</button>
        <button onClick={() => setTheme("dark")}>Set dark Theme</button>
        <GetBySykkelData />
        <GetBusData />
        <Footer />
      </>
    </AppProvider>
  );
}

export default App;
