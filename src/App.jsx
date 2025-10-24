import { useState } from "react";
import "./App.css";
import Footer from "./Components/footer.jsx";
import GetBergenTemp from "./Components/GetBergenTemp.jsx";
import { AppProvider } from "./Components/AppContext";
import TimeDate from "./Components/TimeDate.jsx";
import GetBySykkelData from "./Components/GetBySykkelData.jsx";

function App() {
  return (
    <AppProvider>
      <>
        <h1 className="overskrift">Bergen App</h1>
        <TimeDate />
        <GetBySykkelData />
        <GetBergenTemp />
        <Footer />
      </>
    </AppProvider>
  );
}

export default App;
