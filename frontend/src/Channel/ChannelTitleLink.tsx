import React from 'react';
import Link, { LinkProps } from 'Components/Link/Link';

export interface ChannelTitleLinkProps extends LinkProps {
  titleSlug: string;
  title: string;
}

export default function ChannelTitleLink({
  titleSlug,
  title,
  ...linkProps
}: ChannelTitleLinkProps) {
  const link = `/channels/${titleSlug}`;

  return (
    <Link to={link} {...linkProps}>
      {title}
    </Link>
  );
}
