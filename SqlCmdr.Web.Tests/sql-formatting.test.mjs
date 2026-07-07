import test from 'node:test';
import assert from 'node:assert/strict';

import {
    applySqlFormattingPostProcessors,
    getFormatOptions,
    insertAsForAliases,
    keepJoinOnSameLine,
    moveCommasToLineEnd,
    moveCommasToLineStart
} from '../SqlCmdr.Web/wwwroot/js/sql-formatting.js';

const defaultOptions = getFormatOptions({});

function format(sql, overrides = {}) {
    return applySqlFormattingPostProcessors(sql, { ...defaultOptions, ...overrides });
}

test('default formatting options match SQL Commander style defaults', () => {
    assert.deepEqual(getFormatOptions({}), {
        keywordCase: 'upper',
        functionCase: 'preserve',
        dataTypeCase: 'preserve',
        indentStyle: 'standard',
        logicalOperatorNewline: 'before',
        commaPosition: 'end',
        tabWidth: 4,
        useTabs: false,
        expressionWidth: 50,
        linesBetweenQueries: 1,
        denseOperators: false,
        newlineBeforeSemicolon: false,
        insertAsForAliases: true,
        keepJoinOnSameLine: true
    });
});

test('does not insert AS into TOP clause and adds AS to table alias with semicolon', () => {
    const input = `SELECT
    TOP 100 [Id]
    , [Name] AS name
    , [Description]
FROM
    [dbo].[Categories] c;`;

    const actual = format(input, { commaPosition: 'start' });

    assert.match(actual, /TOP 100 \[Id\]/);
    assert.doesNotMatch(actual, /TOP 100\s+AS\s+\[Id\]/i);
    assert.match(actual, /FROM\n\s+\[dbo\]\.\[Categories\] AS c;/);
});

test('repairs previously inserted AS in TOP clause', () => {
    const input = `SELECT
    TOP 100 AS [Id]
    , [Name]
FROM
    [dbo].[Categories] c;`;

    const actual = format(input, { commaPosition: 'start' });

    assert.match(actual, /TOP 100 \[Id\]/);
    assert.doesNotMatch(actual, /TOP 100\s+AS\s+\[Id\]/i);
    assert.match(actual, /\[dbo\]\.\[Categories\] AS c;/);
});

test('leaves common TOP forms alone when inserting column aliases', () => {
    const topExpressions = [
        'TOP 100 [Id]',
        'TOP (100) [Id]',
        'TOP (@limit) [Id]',
        'TOP 100 PERCENT [Id]',
        'TOP 100 WITH TIES [Id]'
    ];

    for (const topExpression of topExpressions) {
        const actual = format(`SELECT\n    ${topExpression}\nFROM dbo.Users u;`);

        assert.match(actual, new RegExp(topExpression.replace(/[()\[\]@]/g, '\\$&')));
        assert.doesNotMatch(actual, /TOP\b.*\bAS\s+\[Id\]/i);
        assert.match(actual, /FROM dbo\.Users AS u;/);
    }
});

test('adds AS to table aliases including semicolon suffixes', () => {
    assert.equal(insertAsForAliases('FROM dbo.Users u;', defaultOptions), 'FROM dbo.Users AS u;');
    assert.equal(insertAsForAliases('FROM [dbo].[Users] u;', defaultOptions), 'FROM [dbo].[Users] AS u;');
    assert.equal(insertAsForAliases('JOIN dbo.Roles r;', defaultOptions), 'JOIN dbo.Roles AS r;');
    assert.equal(insertAsForAliases('FROM dbo.Users AS u;', defaultOptions), 'FROM dbo.Users AS u;');
    assert.equal(insertAsForAliases('FROM dbo.Users WHERE IsActive = 1', defaultOptions), 'FROM dbo.Users WHERE IsActive = 1');
});

test('adds AS to split FROM and JOIN source lines', () => {
    const input = `FROM
    dbo.Users u
JOIN
    dbo.Roles r`;

    const actual = insertAsForAliases(input, defaultOptions);

    assert.equal(actual, `FROM
    dbo.Users AS u
JOIN
    dbo.Roles AS r`);
});

test('adds AS to column aliases while preserving existing AS and CASE lines', () => {
    const input = `SELECT
    COUNT(*) Total,
    u.Name DisplayName,
    u.Email AS EmailAddress,
    CASE
        WHEN u.Id = 1 THEN u.Name
        ELSE u.Email
    END PreferredName
FROM dbo.Users u;`;

    const actual = format(input);

    assert.match(actual, /COUNT\(\*\) AS Total,/);
    assert.match(actual, /u\.Name AS DisplayName,/);
    assert.match(actual, /u\.Email AS EmailAddress,/);
    assert.match(actual, /WHEN u\.Id = 1 THEN u\.Name/);
    assert.match(actual, /ELSE u\.Email/);
    assert.match(actual, /END AS PreferredName/);
});

test('moves commas to line start and back to line end', () => {
    const endCommaSql = `SELECT
    [Id],
    [Name],
    [Description]
FROM dbo.Categories;`;

    const leading = moveCommasToLineStart(endCommaSql);
    assert.equal(leading, `SELECT
    [Id]
    , [Name]
    , [Description]
FROM dbo.Categories;`);

    assert.equal(moveCommasToLineEnd(leading), endCommaSql);
});

test('keeps ON on same line as JOIN when enabled', () => {
    const input = `SELECT
    u.Id
FROM dbo.Users AS u
JOIN dbo.Roles AS r
ON r.Id = u.RoleId`;

    const actual = keepJoinOnSameLine(input, defaultOptions);

    assert.equal(actual, `SELECT
    u.Id
FROM dbo.Users AS u
JOIN dbo.Roles AS r ON r.Id = u.RoleId`);
});

test('respects disabled alias insertion and JOIN ON options', () => {
    const input = `SELECT
    u.Id UserId
FROM dbo.Users u
JOIN dbo.Roles r
ON r.Id = u.RoleId`;

    const actual = format(input, {
        insertAsForAliases: false,
        keepJoinOnSameLine: false
    });

    assert.equal(actual, input);
});

test('respects lowercase keyword option for inserted AS and ON', () => {
    const input = `SELECT
    u.Id UserId
FROM dbo.Users u
JOIN dbo.Roles r
ON r.Id = u.RoleId`;

    const actual = format(input, { keywordCase: 'lower' });

    assert.match(actual, /u\.Id as UserId/);
    assert.match(actual, /FROM dbo\.Users as u/);
    assert.match(actual, /JOIN dbo\.Roles as r on r\.Id = u\.RoleId/);
});
