"use client";

import { useEffect, useState } from "react";

/** `value`, once it has stopped changing for `delay` ms (for search-as-you-type). */
export function useDebounced<T>(value: T, delay = 200): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay]);
  return debounced;
}
