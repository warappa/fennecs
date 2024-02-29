// markdown-emoji-plugin.ts
import MarkdownIt from 'markdown-it';

interface EmojiPluginOptions {
  baseUrl: string;
  prefix: string;
  postfix: string;
  fileExtension: string;
}

export function neofoxPlugin(md: MarkdownIt, options?: Partial<EmojiPluginOptions>) {
  const defaultOptions: EmojiPluginOptions = {
    baseUrl: 'https://fennecs.tech/img/neofox_',
    prefix: ':neofox_',
    postfix: ':',
    fileExtension: '.png'
  };

  const opts = { ...defaultOptions, ...options };

  md.inline.ruler.after('emphasis', 'custom_emoji', (state, silent): boolean => {
    const startPos = state.pos;

    if (state.src.charAt(startPos) !== opts.prefix.charAt(0)) return false;

    const match = state.src.slice(startPos).match(new RegExp(`^\\${opts.prefix}(.*?)\\${opts.postfix}`));
    if (!match) return false;

    if (!silent) {
      // Instead of creating a Token directly, use the push method to add a new token
      const token = state.push('emoji', '', 0);
      token.content = `${opts.baseUrl}${match[1]}${opts.fileExtension}`;
    }

    state.pos += match[0].length;
    return true;
  });

  md.renderer.rules.emoji = (tokens, idx) => {
    return `<img src="${tokens[idx].content}" alt="Custom emoji" style="display: inline-block; width: 64px; height: auto; margin: 0; padding: 0; vertical-align: middle;" />`;
  };
}
