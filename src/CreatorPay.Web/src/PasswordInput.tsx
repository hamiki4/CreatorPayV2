import { InputHTMLAttributes, useId, useState } from "react";

type Props = Omit<InputHTMLAttributes<HTMLInputElement>, "type" | "value" | "onChange"> & {
  label: string;
  value: string;
  onChange: (value: string) => void;
};

export function PasswordInput({ label, value, onChange, ...input }: Props) {
  const [visible, setVisible] = useState(false), generatedId = useId(), inputId = input.id ?? generatedId;
  return <div className="password-field" style={{ minWidth: 0 }}>
    <label htmlFor={inputId}>{label}</label>
    <span className="password-control" style={{ position: "relative", display: "block", minWidth: 0 }}>
      <input {...input} id={inputId} type={visible ? "text" : "password"} value={value} onChange={event => onChange(event.target.value)} style={{ paddingRight: "3rem", ...input.style }} />
      <button type="button" className="password-toggle" aria-label={visible ? "Hide password" : "Show password"} aria-pressed={visible} onClick={() => setVisible(value => !value)} style={{ position: "absolute", right: ".35rem", top: "50%", transform: "translateY(-50%)", minHeight: "2.25rem", minWidth: "2.25rem", padding: ".35rem", color: "#153c27", background: "transparent" }}>
        <svg aria-hidden="true" viewBox="0 0 24 24" focusable="false">
          {visible
            ? <><path d="M3 3l18 18"/><path d="M10.6 10.7a2 2 0 002.7 2.7M9.9 4.3A10.8 10.8 0 0112 4c5.4 0 9 5 9 5a15.7 15.7 0 01-2.1 2.6M6.6 6.6C4.4 8.1 3 10 3 10s3.6 5 9 5c1.1 0 2.1-.2 3-.5"/></>
            : <><path d="M3 12s3.6-5 9-5 9 5 9 5-3.6 5-9 5-9-5-9-5z"/><circle cx="12" cy="12" r="2.5"/></>}
        </svg>
      </button>
    </span>
  </div>;
}
