import { useState } from "react";
import "./App.css";
import Footer from "./Components/footer.jsx";
import GetBergenTemp from "./Components/GetBergenTemp.jsx";
import { AppProvider } from "./Components/AppContext";
import TimeDate from "./Components/TimeDate.jsx";
import GetBySykkelData from "./Components/GetBySykkelData.jsx";
import GetBusData from "./Components/GetBusData.jsx";

function App() {
  return (
    <AppProvider>
      <>
        <GetBergenTemp />
        <TimeDate />
        <GetBusData />
        <GetBySykkelData />
        <Footer />
      </>
    </AppProvider>
  );
}

export default App;
