type NavIconName = 'discover' | 'cashback' | 'notifications' | 'profile' | 'find' | 'ads' | 'requests' | 'payout' | 'sales' | 'creators' | 'checkout' | 'wallet'

const iconProps = {
  viewBox: '0 0 24 24',
  'aria-hidden': true as const,
}

export function NavIcon({ name }: { name: NavIconName }) {
  switch (name) {
    case 'discover':
    case 'find':
      return (
        <svg {...iconProps}>
          <path d="M11 5a6 6 0 1 0 0 12 6 6 0 0 0 0-12Z" />
          <path d="m20 20-3.5-3.5" />
        </svg>
      )
    case 'cashback':
      return (
        <svg {...iconProps}>
          <path d="M5 7h14v10H5z" />
          <path d="M7 10.5h10" />
          <path d="M12 13a2 2 0 1 0 0-4" />
        </svg>
      )
    case 'notifications':
      return (
        <svg {...iconProps}>
          <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" />
          <path d="M10 21h4" />
        </svg>
      )
    case 'profile':
      return (
        <svg {...iconProps}>
          <path d="M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z" />
          <path d="M4 20a8 8 0 0 1 16 0" />
        </svg>
      )
    case 'ads':
      return (
        <svg {...iconProps}>
          <path d="M4 11h4l7-4v10l-7-4H4z" />
          <path d="M15 9a4 4 0 0 1 0 6" />
        </svg>
      )
    case 'requests':
      return (
        <svg {...iconProps}>
          <path d="M4 6h16v10H7l-3 3z" />
          <path d="M8 10h8" />
          <path d="M8 13h5" />
        </svg>
      )
    case 'payout':
    case 'wallet':
      return (
        <svg {...iconProps}>
          <path d="M4 7h16v10H4z" />
          <path d="M14 11h6" />
          <path d="M15.5 11a1 1 0 1 0 0 2 1 1 0 0 0 0-2Z" />
        </svg>
      )
    case 'sales':
      return (
        <svg {...iconProps}>
          <path d="M5 19V9" />
          <path d="M10 19V5" />
          <path d="M15 19v-7" />
          <path d="M20 19H4" />
        </svg>
      )
    case 'creators':
      return (
        <svg {...iconProps}>
          <path d="M8 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" />
          <path d="M17 12a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z" />
          <path d="M3 19a5 5 0 0 1 10 0" />
          <path d="M13 19a4 4 0 0 1 8 0" />
        </svg>
      )
    case 'checkout':
      return (
        <svg {...iconProps}>
          <path d="M5 7h14l-1.5 9H6.5z" />
          <path d="M8 7V5a4 4 0 0 1 8 0v2" />
          <path d="M9 12h6" />
        </svg>
      )
  }
}
