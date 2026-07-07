const SQL_ALIAS_STOP_WORDS = new Set([
    'all', 'and', 'as', 'asc', 'between', 'by', 'case', 'cross', 'desc', 'else', 'end', 'except', 'for', 'from',
    'full', 'group', 'having', 'in', 'inner', 'intersect', 'join', 'left', 'like', 'not', 'null', 'on', 'or',
    'order', 'outer', 'right', 'then', 'top', 'union', 'when', 'where', 'with'
]);

const CLAUSE_START_PATTERN = /^(FROM|WHERE|GROUP\s+BY|ORDER\s+BY|HAVING|UNION|EXCEPT|INTERSECT|JOIN|LEFT\b|RIGHT\b|INNER\b|FULL\b|CROSS\b|OUTER\b|ON\b|VALUES|SET|INSERT|UPDATE|DELETE)\b/i;
const TABLE_SOURCE_LINE_PATTERN = /^(FROM|JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|INNER\s+JOIN|FULL\s+JOIN|CROSS\s+JOIN|LEFT\s+OUTER\s+JOIN|RIGHT\s+OUTER\s+JOIN|FULL\s+OUTER\s+JOIN|CROSS\s+APPLY|OUTER\s+APPLY)\b\s*$/i;
const TABLE_ALIAS_SUFFIX_PATTERN = '(\\s*(?:;|--.*|(?:ON|WHERE|GROUP\\s+BY|ORDER\\s+BY|HAVING|JOIN|LEFT|RIGHT|INNER|FULL|CROSS|OUTER|UNION)\\b.*)?)';
const SQL_ALIAS_PATTERN = '(\\[[^\\]]+\\]|[A-Za-z_][\\w$#]*)';

export function getFormatOptions(settings = {}) {
    const s = settings || {};
    const oneOf = (value, allowed, fallback) => allowed.includes(value) ? value : fallback;
    const clampInt = (value, min, max, fallback) => {
        const n = parseInt(value, 10);
        return Number.isFinite(n) ? Math.min(max, Math.max(min, n)) : fallback;
    };

    return {
        keywordCase: oneOf(s.formatKeywordCase, ['upper', 'lower', 'preserve'], 'upper'),
        functionCase: oneOf(s.formatFunctionCase, ['upper', 'lower', 'preserve'], 'preserve'),
        dataTypeCase: oneOf(s.formatDataTypeCase, ['upper', 'lower', 'preserve'], 'preserve'),
        indentStyle: oneOf(s.formatIndentStyle, ['standard', 'tabularLeft', 'tabularRight'], 'standard'),
        logicalOperatorNewline: oneOf(s.formatLogicalOperatorPosition, ['before', 'after'], 'before'),
        commaPosition: oneOf(s.formatCommaPosition, ['end', 'start'], 'end'),
        tabWidth: clampInt(s.formatIndentSize, 1, 8, 4),
        useTabs: s.formatUseTabs === true,
        expressionWidth: clampInt(s.formatExpressionWidth, 20, 200, 50),
        linesBetweenQueries: clampInt(s.formatLinesBetweenStatements, 0, 5, 1),
        denseOperators: s.formatDenseOperators === true,
        newlineBeforeSemicolon: s.formatNewlineBeforeSemicolon === true,
        insertAsForAliases: s.formatInsertAsForAliases !== false,
        keepJoinOnSameLine: s.formatKeepJoinOnSameLine !== false
    };
}

export function applySqlFormattingPostProcessors(sql, options = {}) {
    let text = String(sql || '');

    if (options.insertAsForAliases !== false) {
        text = insertAsForAliases(text, options);
    }

    if (options.keepJoinOnSameLine !== false) {
        text = keepJoinOnSameLine(text, options);
    }

    text = applyCommaPlacement(text, options);
    return text.trim();
}

export function formatSqlKeyword(keyword, options = {}) {
    const text = String(keyword || '');
    if (options.keywordCase === 'lower') return text.toLowerCase();
    return options.keywordCase === 'preserve' ? text : text.toUpperCase();
}

export function isSqlAliasStopWord(value) {
    const word = String(value || '').replace(/^\[|\]$/g, '').toLowerCase();
    return SQL_ALIAS_STOP_WORDS.has(word);
}

export function insertAsForAliases(sql, options = {}) {
    const asKeyword = formatSqlKeyword('AS', options);
    let inSelectList = false;
    let pendingTableSourceLine = false;

    return String(sql || '').split('\n').map(line => {
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith('--')) return line;

        if (pendingTableSourceLine) {
            pendingTableSourceLine = false;
            return insertAsForBareTableAliasLine(line, asKeyword);
        }

        if (/^SELECT\b/i.test(trimmed)) {
            inSelectList = true;
            return line;
        }

        if (/^(FROM|WHERE|GROUP\s+BY|ORDER\s+BY|HAVING|UNION|EXCEPT|INTERSECT)\b/i.test(trimmed)) {
            inSelectList = false;
        }

        let nextLine = line;
        if (inSelectList && !CLAUSE_START_PATTERN.test(trimmed)) {
            nextLine = insertAsForColumnAliasLine(nextLine, asKeyword);
        }

        if (TABLE_SOURCE_LINE_PATTERN.test(trimmed)) {
            pendingTableSourceLine = true;
        }

        return insertAsForTableAliasLine(nextLine, asKeyword);
    }).join('\n');
}

export function insertAsForColumnAliasLine(line, asKeyword = 'AS') {
    const trimmed = line.trim();
    if (/^(WHEN|THEN|ELSE)\b/i.test(trimmed)) return line;

    const topNormalized = normalizeTopSelectLine(line);
    if (topNormalized !== line || /^,?\s*TOP\b/i.test(trimmed)) return topNormalized;

    if (/\bAS\b/i.test(trimmed)) return line;

    const match = line.match(/^(\s*,?\s*)(.+?)(\s+)(\[[^\]]+\]|"[^"]+"|[A-Za-z_][\w$#]*)(\s*,?)$/);
    if (!match) return line;

    const [, prefix, expression, , alias, suffix] = match;
    const expressionText = expression.trim();
    if (!expressionText || /\bAS\s*$/i.test(expressionText) || /[+\-*\/=<>!]\s*$/.test(expressionText)) return line;
    if (isSqlAliasStopWord(alias)) return line;

    return `${prefix}${expressionText} ${asKeyword} ${alias}${suffix || ''}`;
}

export function normalizeTopSelectLine(line) {
    const match = line.match(/^(\s*,?\s*TOP\s+(?:\([^)]*\)|@?[A-Za-z_][\w$#]*|\d+)(?:\s+PERCENT)?(?:\s+WITH\s+TIES)?)(\s+)AS\s+(.+)$/i);
    if (!match) return line;

    const [, topExpression, , remainder] = match;
    return `${topExpression} ${remainder}`;
}

export function insertAsForTableAliasLine(line, asKeyword = 'AS') {
    const joinPrefix = '(?:FROM|JOIN|LEFT\\s+JOIN|RIGHT\\s+JOIN|INNER\\s+JOIN|FULL\\s+JOIN|CROSS\\s+JOIN|LEFT\\s+OUTER\\s+JOIN|RIGHT\\s+OUTER\\s+JOIN|FULL\\s+OUTER\\s+JOIN|CROSS\\s+APPLY|OUTER\\s+APPLY)';
    const regex = new RegExp(`^(\\s*${joinPrefix}\\s+)(.+?)(\\s+)${SQL_ALIAS_PATTERN}${TABLE_ALIAS_SUFFIX_PATTERN}$`, 'i');
    const match = line.match(regex);
    if (!match) return line;

    const [, prefix, source, , alias, suffix = ''] = match;
    if (/\bAS\s*$/i.test(source.trim()) || isSqlAliasStopWord(alias)) return line;

    return `${prefix}${source.trimEnd()} ${asKeyword} ${alias}${suffix}`;
}

export function insertAsForBareTableAliasLine(line, asKeyword = 'AS') {
    const regex = new RegExp(`^(\\s*)(.+?)(\\s+)${SQL_ALIAS_PATTERN}${TABLE_ALIAS_SUFFIX_PATTERN}$`, 'i');
    const match = line.match(regex);
    if (!match) return line;

    const [, indent, source, , alias, suffix = ''] = match;
    if (/\bAS\s*$/i.test(source.trim()) || isSqlAliasStopWord(alias)) return line;

    return `${indent}${source.trimEnd()} ${asKeyword} ${alias}${suffix}`;
}

export function keepJoinOnSameLine(sql, options = {}) {
    const lines = String(sql || '').split('\n');
    const result = [];
    const joinLinePattern = /^\s*(?:JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|INNER\s+JOIN|FULL\s+JOIN|CROSS\s+JOIN|LEFT\s+OUTER\s+JOIN|RIGHT\s+OUTER\s+JOIN|FULL\s+OUTER\s+JOIN)\b/i;
    const onLinePattern = /^\s*ON\b\s*(.*)$/i;
    const onKeyword = formatSqlKeyword('ON', options);

    for (let index = 0; index < lines.length; index++) {
        const current = lines[index];
        const next = lines[index + 1] || '';
        const onMatch = next.match(onLinePattern);
        if (joinLinePattern.test(current) && onMatch) {
            const condition = onMatch[1] ? ` ${onMatch[1].trim()}` : '';
            result.push(`${current.trimEnd()} ${onKeyword}${condition}`);
            index++;
            continue;
        }

        result.push(current);
    }

    return result.join('\n');
}

export function applyCommaPlacement(sql, options = {}) {
    return options.commaPosition === 'start'
        ? moveCommasToLineStart(sql)
        : moveCommasToLineEnd(sql);
}

export function moveCommasToLineStart(sql) {
    const lines = String(sql || '').split('\n');
    for (let index = 0; index < lines.length - 1; index++) {
        const current = lines[index];
        const match = current.match(/^(.*\S)\s*,\s*$/);
        if (!match) continue;

        const next = lines[index + 1];
        const nextMatch = next.match(/^(\s*)(.*)$/);
        if (!nextMatch || nextMatch[2].trimStart().startsWith(',')) continue;

        lines[index] = match[1];
        lines[index + 1] = `${nextMatch[1]}, ${nextMatch[2].trimStart()}`;
    }

    return lines.join('\n');
}

export function moveCommasToLineEnd(sql) {
    const lines = String(sql || '').split('\n');
    for (let index = 1; index < lines.length; index++) {
        const match = lines[index].match(/^(\s*),\s*(.*)$/);
        if (!match) continue;

        let previousIndex = index - 1;
        while (previousIndex >= 0 && !lines[previousIndex].trim()) previousIndex--;
        if (previousIndex < 0) continue;

        lines[previousIndex] = `${lines[previousIndex].trimEnd()},`;
        lines[index] = `${match[1]}${match[2]}`;
    }

    return lines.join('\n');
}

const sqlCommanderFormatting = {
    getFormatOptions,
    applySqlFormattingPostProcessors,
    formatSqlKeyword,
    isSqlAliasStopWord,
    insertAsForAliases,
    insertAsForColumnAliasLine,
    normalizeTopSelectLine,
    insertAsForTableAliasLine,
    insertAsForBareTableAliasLine,
    keepJoinOnSameLine,
    applyCommaPlacement,
    moveCommasToLineStart,
    moveCommasToLineEnd
};

globalThis.SqlCommanderFormatting = sqlCommanderFormatting;

export default sqlCommanderFormatting;
