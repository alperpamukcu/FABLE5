# Achievement pacing — the floor bot

Written by `LastCall → Achievement Pacing`. **200** seeded bars, horizon 90 nights, each
with a fresh ledger (a new player's first bar). The bot is a FLOOR: it plays at one standard,
never learns, and goes bankrupt young — so a late achievement it does not reach is expected, an
early one it does not reach is a design bug. Nights are counted from 1; a bar's night is the
one whose books just closed (the market comes before, so purchases count on their own night).

Nights played p25/median/p75: 17 / 21 / 22.
Longest run of nights with nothing earned, p25/median/p75: 2 / 4 / 5.

## How many land on each night

| night | bars still open | unlocks per open bar | earned by then (per bar) |
|--:|--:|--:|--:|
| 1 | 200 | 3.03 | 3.03 |
| 2 | 200 | 1.96 | 4.99 |
| 3 | 200 | 0.99 | 5.97 |
| 4 | 200 | 2.41 | 8.38 |
| 5 | 200 | 1.71 | 10.09 |
| 6 | 200 | 1.79 | 11.88 |
| 7 | 200 | 1.70 | 13.57 |
| 8 | 200 | 0.94 | 14.51 |
| 9 | 200 | 1.10 | 15.61 |
| 10 | 200 | 0.71 | 16.32 |
| 11 | 200 | 1.07 | 17.39 |
| 12 | 200 | 1.32 | 18.71 |
| 13 | 200 | 0.25 | 18.96 |
| 14 | 200 | 0.22 | 19.17 |
| 15 | 200 | 0.68 | 19.85 |
| 16 | 200 | 0.63 | 20.48 |
| 17 | 189 | 0.33 | 20.79 |
| 18 | 142 | 0.20 | 20.93 |
| 19 | 126 | 0.06 | 20.97 |
| 20 | 121 | 0.09 | 21.03 |
| 21 | 115 | 0.23 | 21.16 |
| 22 | 94 | 0.71 | 21.49 |
| 23 | 29 | 0.97 | 21.63 |
| 24 | 2 | 2.00 | 21.65 |

## Each achievement

| id | tier | name | bars that earned it | night: p25 / median / p75 |
|---|---|---|--:|--:|
| FIRST_ROUND | opening | First Round | 200 of 200 (100.00%) | 1 / 1 / 1 |
| LIGHTS_OUT | opening | Lights Out | 200 of 200 (100.00%) | 1 / 1 / 1 |
| A_NEW_PAGE | opening | A New Page | 200 of 200 (100.00%) | 2 / 2 / 2 |
| HARD_SHAKE | week | Hard Shake | 200 of 200 (100.00%) | 4 / 4 / 4 |
| GOOD_HEADS | week | Good Heads | 200 of 200 (100.00%) | 4 / 5 / 5 |
| GETTING_THE_HANG | week | Getting the Hang of It | 200 of 200 (100.00%) | 3 / 4 / 4 |
| ELBOW_GREASE | week | Elbow Grease | 200 of 200 (100.00%) | 6 / 7 / 7 |
| SAME_AGAIN | week | Same Again | 200 of 200 (100.00%) | 6 / 7 / 9 |
| STOCKING_UP | week | Stocking Up | 200 of 200 (100.00%) | 5 / 5 / 5 |
| HOME_IMPROVEMENT | week | Home Improvement | 200 of 200 (100.00%) | 1 / 1 / 1 |
| TALK_OF_THE_STREET | week | Talk of the Street | 200 of 200 (100.00%) | 9 / 10 / 10 |
| SIX_NIGHTS | week | Six Nights a Week | 200 of 200 (100.00%) | 6 / 6 / 6 |
| THE_USUAL | week | The Usual | 200 of 200 (100.00%) | 2 / 2 / 3 |
| A_WEEKS_WORK | week | A Job Well Done | 200 of 200 (100.00%) | 3 / 3 / 4 |
| THE_BLOCKS_BEST | early | The Block's Best | 82 of 200 (41.00%) | 12 / 13 / 13 |
| NOT_TONIGHT | early | Not Tonight | 69 of 200 (34.50%) | 14 / 15 / 16 |
| HUNDRED_CLUB | early | Hundred Club | 200 of 200 (100.00%) | 11 / 11 / 11 |
| TIP_JAR | early | Tip Jar | 200 of 200 (100.00%) | 15 / 15 / 16 |
| CLEAN_SHEET | early | Clean Sheet | 195 of 200 (97.50%) | 8 / 9 / 9 |
| TWO_MORE_STOOLS | early | Two More Stools | 200 of 200 (100.00%) | 7 / 7 / 7 |
| A_FORTNIGHT_OPEN | early | A Fortnight Open | 200 of 200 (100.00%) | 12 / 12 / 12 |
| LAST_ORDERS | early | Last Orders | 200 of 200 (100.00%) | 17 / 21 / 22 |
| CONFIDANT | early | Confidant | 169 of 200 (84.50%) | 8 / 10 / 14 |
| TALK_OF_THE_TOWN | mid | Talk of the Town | 0 of 200 (0.00%) | — |
| STIRRED_NOT_SHAKEN | mid | Stirred, Not Shaken | 0 of 200 (0.00%) | — |
| WELL_READ | mid | Well Read | 0 of 200 (0.00%) | — |
| DOWN_TO_THE_DROP | mid | Down to the Drop | 12 of 200 (6.00%) | 16 / 17 / 17 |
| THE_CITYS_BEST | mid | The City's Best | 0 of 200 (0.00%) | — |
| SEEN_THEM_ALL | mid | Seen Them All | 1 of 200 (0.50%) | 16 / 16 / 16 |
| BIG_NIGHT | mid | Big Night | 0 of 200 (0.00%) | — |
| TAPMASTER | mid | Tapmaster | 0 of 200 (0.00%) | — |
| THE_BOUNCER | mid | The Bouncer | 0 of 200 (0.00%) | — |
| FOUR_WEEKS_OPEN | mid | Four Weeks Open | 2 of 200 (1.00%) | 24 / 24 / 24 |
| NIGHT_OWL | mid | Night Owl | 0 of 200 (0.00%) | — |
| FIVE_HUNDRED_POURS | mid | Five Hundred Pours | 0 of 200 (0.00%) | — |
| DISH_PIG | mid | Dish Pig | 0 of 200 (0.00%) | — |
| TEN_OUT_OF_TEN | late | Ten Out of Ten | 0 of 200 (0.00%) | — |
| A_THOUSAND_POURS | late | A Thousand Pours | 0 of 200 (0.00%) | — |
| THE_COUNTRYS_BEST | late | The Country's Best | 0 of 200 (0.00%) | — |
| FIVE_STARS_TONIGHT | late | Five Stars Tonight | 0 of 200 (0.00%) | — |
| MONEY_IN_THE_BANK | late | Money in the Bank | 0 of 200 (0.00%) | — |
| LEGENDARY_GLASSWARE | late | Legendary Glassware | 0 of 200 (0.00%) | — |
| THE_BEST_THERE_IS | late | The Best There Is | 0 of 200 (0.00%) | — |
| FIVE_STAR_ROOM | late | Five-Star Room | 0 of 200 (0.00%) | — |
| THE_WHOLE_BOOK | late | The Whole Book | 0 of 200 (0.00%) | — |
| AN_INSTITUTION | late | An Institution | 0 of 200 (0.00%) | — |
| WRONG_CALL | secret | Wrong Call | 0 of 200 (0.00%) | — |
| BLOWOUT | secret | Blowout | 0 of 200 (0.00%) | — |
