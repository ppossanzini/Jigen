import { cn } from '@/lib/utils'

type ConfigSidebarProps = React.HTMLAttributes<HTMLElement>

export function ConfigSidebar({
  className,
  children,
  ...props
}: ConfigSidebarProps) {
  return (
    <aside
      className={cn(
        'w-full shrink-0 space-y-4 overflow-y-auto rounded-md border p-4 lg:w-80',
        className
      )}
      {...props}
    >
      {children}
    </aside>
  )
}
