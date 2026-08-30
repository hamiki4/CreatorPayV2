import type {SVGProps} from 'react'

export type NavIconName =
  | 'home'
  | 'discover'
  | 'cashback'
  | 'notifications'
  | 'profile'
  | 'find'
  | 'ads'
  | 'requests'
  | 'payout'
  | 'sales'
  | 'creators'
  | 'checkout'
  | 'wallet'
  | 'userPlus'
  | 'cashier'
  | 'settings'
  | 'copy'
  | 'mapPin'
  | 'video'

type NavIconProps = {
  name: NavIconName
  size?: number
  className?: string
} & Pick<SVGProps<SVGSVGElement>, 'aria-label'>

const iconProps: SVGProps<SVGSVGElement> = {
  viewBox: '0 0 24 24',
  'aria-hidden': true as const,
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 2.35,
  strokeLinecap: 'round',
  strokeLinejoin: 'round',
}

export function NavIcon({name, size = 22, className}: NavIconProps) {
  const svgProps = { ...iconProps, width: size, height: size, className }

  switch (name) {
    case 'home':
      return (
        <svg {...svgProps}>
          <path d="m3.5 10 8.5-7 8.5 7" />
          <path d="M5.5 9v11h13V9" />
          <path d="M9.5 20v-6h5v6" />
        </svg>
      )
    case 'discover':
    case 'find':
      return (
        <svg {...svgProps}>
          <circle cx="11" cy="11" r="5.75" />
          <path d="M15.25 15.25 20 20" />
          <path d="M8.8 11h4.4" />
        </svg>
      )
    case 'cashback':
      return (
        <svg {...svgProps}>
          <path d="M5 7.5h14v9H5z" />
          <path d="M9 10.2h6" />
          <path d="M12 13.2v-4.5" />
          <path d="M10.1 9.2 12 7.5l1.9 1.7" />
        </svg>
      )
    case 'notifications':
      return (
        <svg {...svgProps}>
          <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" />
          <path d="M10 21h4" />
        </svg>
      )
    case 'profile':
      return (
        <svg {...svgProps}>
          <path d="M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z" />
          <path d="M4.5 20a7.5 7.5 0 0 1 15 0" />
        </svg>
      )
    case 'ads':
      return (
        <svg {...svgProps}>
          <path d="M4 11h4l7-4v10l-7-4H4z" />
          <path d="M15 9a4 4 0 0 1 0 6" />
        </svg>
      )
    case 'requests':
      return (
        <svg {...svgProps}>
          <path d="M4.5 6.5h15v8.5H8.25L4.5 19V6.5Z" />
          <path d="M8 10h8" />
          <path d="M8 13h5" />
        </svg>
      )
    case 'payout':
    case 'wallet':
      return (
        <svg {...svgProps}>
          <path d="M4.5 7.5h15v9h-15z" />
          <path d="M14 11.5h5.5" />
          <circle cx="15.5" cy="11.5" r="1" />
        </svg>
      )
    case 'sales':
      return (
        <svg {...svgProps}>
          <path d="M5 19V9" />
          <path d="M10 19V5" />
          <path d="M15 19v-7" />
          <path d="M20 19H4" />
        </svg>
      )
    case 'creators':
      return (
        <svg {...svgProps}>
          <path d="M8 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" />
          <path d="M17 12a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z" />
          <path d="M3 19a5 5 0 0 1 10 0" />
          <path d="M13 19a4 4 0 0 1 8 0" />
        </svg>
      )
    case 'checkout':
      return (
        <svg {...svgProps}>
          <path d="M5 7.5h14v9H5z" />
          <path d="M8 7.5V5.8A2.8 2.8 0 0 1 10.8 3h2.4A2.8 2.8 0 0 1 16 5.8v1.7" />
          <path d="M8.5 12h7" />
          <path d="M8.5 15h3.5" />
        </svg>
      )
    case 'userPlus':
      return (
        <svg {...svgProps}>
          <circle cx="9" cy="8" r="3.25" />
          <path d="M3.75 19a5.25 5.25 0 0 1 10.5 0" />
          <path d="M18 8v6M15 11h6" />
        </svg>
      )
    case 'cashier':
      return (
        <svg {...svgProps}>
          <rect x="4" y="5" width="16" height="14" rx="1.5" />
          <path d="M8 5V3h8v2M7.5 9h4M7.5 12h4M15.5 10.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3ZM13.5 15.5a2 2 0 0 1 4 0" />
        </svg>
      )
    case 'settings':
      return (
        <svg {...svgProps}>
          <circle cx="12" cy="12" r="3.25" />
          <path d="M19.1 13.5a7.4 7.4 0 0 0 0-3l2-1.55-2-3.45-2.45 1a8 8 0 0 0-2.6-1.5L13.7 2h-4l-.35 3a8 8 0 0 0-2.6 1.5l-2.45-1-2 3.45 2 1.55a7.4 7.4 0 0 0 0 3l-2 1.55 2 3.45 2.45-1a8 8 0 0 0 2.6 1.5l.35 3h4l.35-3a8 8 0 0 0 2.6-1.5l2.45 1 2-3.45-2-1.55Z" />
        </svg>
      )
    case 'copy':
      return (
        <svg {...svgProps}>
          <rect x="8" y="8" width="11" height="11" rx="2" />
          <path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2" />
        </svg>
      )
    case 'mapPin':
      return (
        <svg {...svgProps}>
          <path d="M20 10c0 5-8 12-8 12S4 15 4 10a8 8 0 1 1 16 0Z" />
          <circle cx="12" cy="10" r="2.4" />
        </svg>
      )
    case 'video':
      return (
        <svg {...svgProps}>
          <rect x="3" y="5" width="14" height="14" rx="3" />
          <path d="m17 10 4-2v8l-4-2" />
          <path d="m9 9 4 3-4 3Z" />
        </svg>
      )
  }
}
