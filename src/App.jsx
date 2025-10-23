import { useState } from "react";
import "./App.css";
import Footer from "./Components/footer.jsx";
import GetBergenTemp from "./Components/GetBergenTemp.jsx";
import { AppProvider } from "./Components/AppContext";
import TimeDate from "./Components/TimeDate.jsx";

function App() {
  return (
    <AppProvider>
      <>
        <TimeDate />
        <p>Bergen App</p>
        <GetBergenTemp />
        <Footer />
      </>
    </AppProvider>
  );
}

export default App;
