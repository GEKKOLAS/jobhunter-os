import * as React from "react"

import { cn } from "@/lib/utils"

/** A plain <select> dressed like <Input>, for short option lists where a popover is overkill. */
function NativeSelect({ className, ...props }: React.ComponentProps<"select">) {
  return (
    <select
      data-slot="native-select"
      className={cn(
        "h-8 w-full min-w-0 rounded-lg border-2 border-input bg-card px-2 text-sm transition-colors outline-none focus-visible:border-ring focus-visible:shadow-hard-sm disabled:cursor-not-allowed disabled:opacity-50 dark:border dark:bg-input/30 dark:focus-visible:shadow-none dark:focus-visible:ring-3 dark:focus-visible:ring-ring/50 [&>option]:bg-popover [&>option]:text-popover-foreground",
        className
      )}
      {...props}
    />
  )
}

export { NativeSelect }
