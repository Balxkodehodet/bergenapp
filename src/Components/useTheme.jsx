import { useState, useEffect } from "react";
export default function useTheme() {
  const [theme, setTheme] = useState(() => {
    return localStorage.getItem("theme") || "dark";
  });

  useEffect(() => {
    const themeChanger = document.documentElement;
    themeChanger.classList.remove("dark-theme", "light-theme");

    if (theme === "dark") themeChanger.classList.add("dark-theme");
    if (theme === "light") themeChanger.classList.add("light-theme");

    localStorage.setItem("theme", theme);
  }, [theme]);
  return [theme, setTheme];
}
