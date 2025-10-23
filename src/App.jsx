import { useState } from "react";
import "./App.css";
import Footer from "./Components/footer.jsx";
import GetBergenTemp from "./Components/GetBergenTemp.jsx";

function App() {
  return (
    <>
      <p>Bergen App</p>
      <GetBergenTemp />
      <Footer />
    </>
  );
}

export default App;
