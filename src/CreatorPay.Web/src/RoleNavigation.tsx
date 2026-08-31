import {NavIcon, type NavIconName} from './navIcons'

export type RoleNavItem = {
  id: string
  label: string
  icon: NavIconName
  active: boolean
  onSelect: () => void
}

export function RoleNavigation({
  role,
  label,
  items,
}: {
  role: 'Customer' | 'Creator' | 'Business'
  label: string
  items: RoleNavItem[]
}) {
  const activeItemId = items.find((item) => item.active)?.id

  return (
    <nav className={`workspace-nav workspace-nav--${role.toLowerCase()}`} aria-label={label}>
      {items.map((item) => {
        const isActive = item.id === activeItemId
        return (
          <button
            key={item.id}
            type="button"
            className={isActive ? 'is-active' : ''}
            aria-current={isActive ? 'page' : undefined}
            onClick={() => {
              item.onSelect()
              window.scrollTo(0, 0)
            }}
          >
            <span className="workspace-nav-icon" aria-hidden="true">
              <NavIcon name={item.icon} size={24} />
            </span>
            <span className="workspace-nav-label">{item.label}</span>
          </button>
        )
      })}
    </nav>
  )
}
